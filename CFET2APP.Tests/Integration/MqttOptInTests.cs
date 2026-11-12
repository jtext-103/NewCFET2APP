using System.Net;
using System.Net.Sockets;
using Jtext103.CFET2.CFET2App.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Jtext103.CFET2.CFET2App.Tests.Integration;

[TestClass, DoNotParallelize, TestCategory("StandardHostIntegration")]
public sealed class MqttOptInTests
{
    [TestMethod]
    [DataRow("defaults", false)]
    [DataRow("ws-with-broker", false)]
    [DataRow("mqtt-publish", true)]
    [DataRow("mqtt-subscribe", true)]
    [DataRow("dual-publish", true)]
    public async Task ProtocolSelectionControlsModuleAndBrokerConnection(string mode, bool enabled)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = limit.Token;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var brokerPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        // A TCP listener is sufficient to detect any attempted broker connection;
        // this test does not require a running MQTT broker.
        var connection = listener.AcceptTcpClientAsync(ct).AsTask();
        await using var app = await MQTTStandardHostProcess.Start(brokerPort, false, true, configure: config =>
        {
            config["HttpEventSubscriptionTransport"] = "WebSocket";
            if (mode == "defaults")
            {
                foreach (var name in config.Properties().Select(p => p.Name)
                    .Where(n => n.StartsWith("Mqtt", StringComparison.Ordinal)
                        || n == "EventPublishTransports" || n == "HttpEventSubscriptionTransport").ToArray())
                    config.Remove(name);
            }
            else if (mode == "mqtt-publish") config["EventPublishTransports"] = new JArray("Mqtt");
            else if (mode == "mqtt-subscribe") config["HttpEventSubscriptionTransport"] = "Mqtt";
            else if (mode == "dual-publish") config["EventPublishTransports"] = new JArray("WebSocket", "Mqtt");
        });
        await app.WaitStarted(ct);
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
        var root = JObject.Parse(await http.GetStringAsync(app.Origin + "/", ct));
        var children = root["ChildrenPath"]!.Values<string>().ToArray();
        Assert.IsTrue(children.Contains("/WebSocketCM"));
        Assert.AreEqual(enabled, children.Contains("/MQTTTCM"));
        Assert.AreEqual(1, (await app.Read("/device/Value", ct)).Value<int>());
        Assert.IsNotNull(await app.Read("/WebSocketCM/status", ct));
        if (enabled)
        {
            using var client = await connection.WaitAsync(ct);
            var status = (JObject)await app.Read("/MQTTTCM/status", ct);
            Assert.IsTrue(status["ConnectionAttempts"]!.Value<int>() > 0);
        }
        else
        {
            await Task.Delay(1200, ct);
            Assert.IsFalse(connection.IsCompletedSuccessfully, "WS-only startup must not connect to the configured broker.");
        }
        await limit.CancelAsync();
        try { using var pendingClient = await connection; }
        catch (OperationCanceledException) { }
    }
}
