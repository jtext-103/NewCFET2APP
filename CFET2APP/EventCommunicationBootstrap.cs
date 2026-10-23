using System;
using System.Linq;
using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Config;
using Jtext103.CFET2.MQTTCommunicationModule.Diagnostics;
using Jtext103.CFET2.MQTTCommunicationModule.Options;
using Jtext103.CFET2.WebSocketCommunicationModule.Diagnostics;
using Jtext103.CFET2.WebSocketCommunicationModule.Options;
using MqttModule = Jtext103.CFET2.MQTTCommunicationModule.MQTTCommunicationModule;
using WsModule = Jtext103.CFET2.WebSocketCommunicationModule.WebSocketCommunicationModule;

namespace Jtext103.CFET2.CFET2App
{
    internal static class EventCommunicationBootstrap
    {
        internal static WebSocketCommunicationOptions CreateWebSocketOptions(EventCommunicationConfiguration configuration,
            string httpHost, int httpPort, TimeSpan resubscribeInterval)
        {
            var enableServer = configuration.EventPublishTransports.Contains(EventTransport.WebSocket);
            return new WebSocketCommunicationOptions
            {
                EnableServer = enableServer,
                ListenUri = enableServer ? new Uri(httpHost + ":" + configuration.GetWebSocketPort(httpPort)) : null,
                ResubscribeInterval = resubscribeInterval
            };
        }
        internal static MQTTCommunicationOptions CreateMqttOptions(EventCommunicationConfiguration configuration)
        {
            configuration.EnsureStandardHostSupported();
            var mqtt = configuration.Mqtt;
            var options = new MQTTCommunicationOptions
            {
                BrokerHost = mqtt.BrokerHost, BrokerPort = mqtt.BrokerPort,
                SourceHttpBaseUri = mqtt.SourceHttpBaseUri,
                PublishLocalEvents = configuration.EventPublishTransports.Contains(EventTransport.Mqtt),
                KeepAlivePeriod = TimeSpan.FromSeconds(mqtt.KeepAliveSeconds),
                OperationTimeout = TimeSpan.FromSeconds(mqtt.OperationTimeoutSeconds),
                DemandRenewInterval = TimeSpan.FromSeconds(mqtt.DemandRenewIntervalSeconds),
                ResourceLeaseTimeout = TimeSpan.FromSeconds(mqtt.ResourceLeaseTimeoutSeconds),
                LeaseScanInterval = TimeSpan.FromSeconds(mqtt.LeaseScanIntervalSeconds)
            };
            return mqtt.ClientId == null ? options : options with { ClientId = mqtt.ClientId };
        }
        internal static void Register(Hub hub, WebSocketCommunicationOptions wsOptions)
        {
            var mqtt = new MqttModule(CreateMqttOptions(hub.ActiveEventCommunicationConfiguration));
            var owned = false;
            try
            {
                var ws = new WsModule(wsOptions);
                hub.TryAddCommunicationModule(ws);
                hub.TryAddThing(new WebSocketCMThing(ws), "/", "WebSocketCM");
                hub.TryAddCommunicationModule(mqtt); owned = true;
                hub.TryAddThing(new MQTTTCMThing(mqtt), "/", "MQTTTCM");
            }
            finally { if (!owned) mqtt.Dispose(); }
        }
    }
}
