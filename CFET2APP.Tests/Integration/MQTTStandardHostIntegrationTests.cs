using System.Collections.Concurrent;
using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Config;
using Jtext103.CFET2.Core.Event;
using Jtext103.CFET2.MQTTCommunicationModule.Diagnostics;
using Jtext103.CFET2.CFET2App.Tests.TestSupport;
using Jtext103.CFET2.WebSocketCommunicationModule.Diagnostics;
using Jtext103.CFET2.WebSocketCommunicationModule.Options;
using Jtext103.CFET2.CFET2App;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Jtext103.CFET2.CFET2App.Tests.Integration;

[TestClass, DoNotParallelize, TestCategory("StandardHostIntegration")]
public sealed class MQTTStandardHostIntegrationTests
{
    private static void Require()
    { if (Environment.GetEnvironmentVariable("CFET_MQTT_INTEGRATION") != "1") Assert.Inconclusive("Enable real standard APP integration tests."); }
    private static async Task Wait(Func<bool> predicate, CancellationToken ct)
    { while (!predicate()) await Task.Delay(15, ct); }

    [TestMethod] public async Task MqttOnlyStandardAppDoesNotRequireLocalWsListenPort()
    {
        Require(); using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(25)); var ct = limit.Token;
        using var broker = await MQTTIsolatedBroker.Create("allow_anonymous true\n", ct);
        await using var app = await MQTTStandardHostProcess.Start(broker.Port, true, false, wsOffset: 65535);
        await app.WaitStarted(ct);
        var status = await app.WaitStatus(s => s["IsConnected"]!.Value<bool>() && s["LocalResourcesReady"]!.Value<bool>(), ct);
        Assert.IsTrue(status["PublishLocalEvents"]!.Value<bool>());
        Assert.AreEqual(1, (await app.Read("/device/Value", ct)).Value<int>());
        Assert.IsNotNull(await app.Read("/WebSocketCM/status", ct));
    }

    [TestMethod]
    [DataRow(EventTransport.Mqtt, true)] [DataRow(EventTransport.WebSocket, true)] [DataRow(EventTransport.Mqtt, false)]
    public async Task StandardAppPublishesAndHttpSubscriptionUsesSelectedTransport(EventTransport selected, bool dual)
    {
        Require(); using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(45)); var ct = limit.Token;
        using var broker = await MQTTIsolatedBroker.Create("allow_anonymous true\n", ct);
        await using var source = await MQTTStandardHostProcess.Start(broker.Port, true, dual);
        await source.WaitStarted(ct);
        var sourceStatus = await source.WaitStatus(s => s["IsConnected"]!.Value<bool>() && s["LocalResourcesReady"]!.Value<bool>(), ct);
        Assert.IsTrue(sourceStatus["PublishLocalEvents"]!.Value<bool>());
        Assert.AreEqual(1, (await source.Read("/device/Value", ct)).Value<int>());
        foreach (var path in new[] { "printStatus", "subscriptions", "printSubscriptions", "observations", "printObservations" })
            Assert.IsNotNull(await source.Read("/MQTTTCM/" + path, ct));
        var saved = GlobalConfig.Get(); var host = new CFET2Host(); var mounted = false;
        var receiverPort = MQTTStandardHostProcess.FreePort();
        while (receiverPort + source.WebSocketOffset > 65535) receiverPort = MQTTStandardHostProcess.FreePort();
        using var http = new Jtext103.CFET2.HTTPCommunicationModule.HTTPCommunicationModule(new Uri("http://127.0.0.1:" + receiverPort));
        try
        {
            var config = new Dictionary<string, string> { ["IP"] = "http://127.0.0.1", ["Port"] = receiverPort.ToString(), ["Accept"] = "Json",
                ["HttpEventSubscriptionTransport"] = selected.ToString(), ["WebSocketPortOffset"] = source.WebSocketOffset.ToString(),
                ["MqttBrokerHost"] = "127.0.0.1", ["MqttBrokerPort"] = broker.Port.ToString() };
            GlobalConfig.Set(config); HubMaster.InjectHubToModule(host); mounted = true;
            host.MyHub.TryAddCommunicationModule(http);
            EventCommunicationBootstrap.Register(host.MyHub, new WebSocketCommunicationOptions
                { ListenUri = new Uri("http://127.0.0.1:" + (receiverPort + source.WebSocketOffset)), EnableServer = true });
            var deliveries = new ConcurrentQueue<string>(); var callbackErrors = new ConcurrentQueue<Exception>();
            void Receive(string label, EventArg ev)
            {
                try { if (ev.Sample.GetVal<int>() == 4242) { Assert.IsTrue(ev.Sample.IsRemote); deliveries.Enqueue(label); } }
                catch (Exception ex) { callbackErrors.Enqueue(ex); }
            }
            using var byHttp = host.MyHub.SubscribeResource(source.Origin + "/device/Value", ev => Receive("http", ev));
            Assert.AreEqual(1, byHttp.InitialValue.GetVal<int>());
            using var byMqtt = host.MyHub.Subscribe(new EventFilter("/device/Value", "changed", 0, source.Origin.Replace("http://", "mqtt://")), ev => Receive("mqtt", ev));
            var wsOrigin = "ws://127.0.0.1:" + (new Uri(source.Origin).Port + source.WebSocketOffset);
            using var byWs = dual ? host.MyHub.Subscribe(new EventFilter("/device/Value", "changed", 0, wsOrigin), ev => Receive("ws", ev)) : null;
            host.MyHub.StartCommunication(); host.MyHub.StartThings();
            IReadOnlyList<MQTTSubscriptionStatus> MqttSubscriptions() => (IReadOnlyList<MQTTSubscriptionStatus>)host.MyHub.TryGetResourceSampleWithUri("/MQTTTCM/subscriptions").ObjectVal;
            IReadOnlyList<WebSocketRemoteClientStatus> WsClients() => (IReadOnlyList<WebSocketRemoteClientStatus>)host.MyHub.TryGetResourceSampleWithUri("/WebSocketCM/remoteClients").ObjectVal;
            var mqttCount = selected == EventTransport.Mqtt ? 2 : 1; var wsCount = dual ? 1 : 0; // Identical rules share one subscription on the source WS server.
            await Wait(() => MqttSubscriptions().Count == mqttCount && MqttSubscriptions().All(s => s.Rules.All(r => r.State == MQTTSubscriptionRuleState.Ready)) &&
                WsClients().Sum(c => c.ActiveSubscriptionCount) == wsCount, ct);
            var before = (MQTTModuleStatus)host.MyHub.TryGetResourceSampleWithUri("/MQTTTCM/status").ObjectVal;
            Assert.IsFalse(before.PublishLocalEvents);
            await source.Publish(4242, ct);
            await Wait(() => !callbackErrors.IsEmpty || deliveries.Count >= (dual ? 3 : 2), ct);
            Assert.IsTrue(callbackErrors.IsEmpty, string.Join(Environment.NewLine, callbackErrors));
            CollectionAssert.AreEquivalent(dual ? new[] { "http", "mqtt", "ws" } : new[] { "http", "mqtt" }, deliveries.ToArray());
            Assert.AreEqual(mqttCount, MqttSubscriptions().Count);
            Assert.IsTrue(((JObject)await source.Read("/MQTTTCM/status", ct))["Publisher"]!["Completed"]!.Value<long>() >= 1);
        }
        finally { if (mounted) Hub.KillMaster(); GlobalConfig.Set(saved); }
    }
    [TestMethod] public async Task SubscriptionOnlyStandardAppStartsWithoutBrokerAndReconnectsWhenItReturns()
    {
        Require(); using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var ct = limit.Token;
        using var broker = await MQTTIsolatedBroker.Create("allow_anonymous true\n", ct); broker.Stop();
        await using var app = await MQTTStandardHostProcess.Start(broker.Port, false, true);
        await app.WaitStarted(ct);
        var offline = await app.WaitStatus(s => s["LocalResourcesReady"]!.Value<bool>() == false && !s["IsConnected"]!.Value<bool>(), ct);
        Assert.IsFalse(offline["PublishLocalEvents"]!.Value<bool>()); Assert.AreEqual(JTokenType.Null, offline["Publisher"]!.Type);
        Assert.IsEmpty((JArray)((JObject)await app.Read("/MQTTTCM/observations", ct))["Resources"]!);
        Assert.AreEqual(1, (await app.Read("/device/Value", ct)).Value<int>());
        await broker.Start(ct); await app.WaitStatus(s => s["IsConnected"]!.Value<bool>(), ct);
    }
    [TestMethod] public async Task InvalidMqttConfigurationTerminatesStandardAppBeforeHttpStarts()
    {
        Require(); using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var app = await MQTTStandardHostProcess.Start(1883, true, false, invalid: true);
        await app.AssertRejected(limit.Token);
    }
}
