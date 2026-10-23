using Jtext103.CFET2.Core.Config;
using Jtext103.CFET2.CFET2App;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Jtext103.CFET2.CFET2App.Tests.HostConfiguration;

[TestClass, DoNotParallelize, TestCategory("HostConfiguration")]
public sealed class EventCommunicationBootstrapTests
{
    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void LocalWsPortIsRequiredOnlyWhenServerIsEnabled(bool publishWs)
    {
        var transports = publishWs ? new[] { EventTransport.Mqtt, EventTransport.WebSocket } : new[] { EventTransport.Mqtt };
        var config = new EventCommunicationConfiguration(transports, EventTransport.Mqtt, 1,
            new(sourceHttpBaseUri: "http://source:65535"));
        if (publishWs)
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EventCommunicationBootstrap.CreateWebSocketOptions(
                config, "http://source", 65535, TimeSpan.FromMilliseconds(50)));
        else
        {
            var options = EventCommunicationBootstrap.CreateWebSocketOptions(config, "http://source", 65535, TimeSpan.FromMilliseconds(50));
            Assert.IsFalse(options.EnableServer); Assert.IsNull(options.ListenUri);
            Assert.AreEqual(TimeSpan.FromMilliseconds(50), options.ResubscribeInterval);
        }
    }

    [TestMethod] public void BootstrapMapsImmutableConfigurationAndGeneratesClientIdOnlyForModuleOptions()
    {
        var config = new EventCommunicationConfiguration([EventTransport.WebSocket, EventTransport.Mqtt], EventTransport.Mqtt, 1,
            new("broker", 1884, null, "http://source:8000", 0, 20, 60, 2, 5));
        var first = EventCommunicationBootstrap.CreateMqttOptions(config); var second = EventCommunicationBootstrap.CreateMqttOptions(config);
        first.Validate(); Assert.IsTrue(first.PublishLocalEvents); Assert.IsNull(config.Mqtt.ClientId);
        Assert.AreNotEqual(first.ClientId, second.ClientId); Assert.AreEqual("broker", first.BrokerHost);
        Assert.AreEqual(1884, first.BrokerPort); Assert.AreEqual(TimeSpan.Zero, first.KeepAlivePeriod);
        Assert.AreEqual(TimeSpan.FromSeconds(20), first.DemandRenewInterval); Assert.AreEqual(TimeSpan.FromSeconds(60), first.ResourceLeaseTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(2), first.LeaseScanInterval); Assert.AreEqual(TimeSpan.FromSeconds(5), first.OperationTimeout);
        var receive = new EventCommunicationConfiguration([EventTransport.WebSocket], EventTransport.Mqtt, 1, new(clientId: "fixed"));
        Assert.IsFalse(EventCommunicationBootstrap.CreateMqttOptions(receive).PublishLocalEvents);
        Assert.AreEqual("fixed", EventCommunicationBootstrap.CreateMqttOptions(receive).ClientId);
    }
}
