using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.BasicThings;
using Jtext103.CFET2.Core.Middleware.Basic;
using Jtext103.CFET2.CFET2App.DynamicLoad;
using Jtext103.CFET2.Core.Config;
using Jtext103.CFET2.HTTPCommunicationModule;
using Jtext103.CFET2.WebSocketCommunicationModule;
using Jtext103.CFET2.WebSocketCommunicationModule.Diagnostics;
using Jtext103.CFET2.WebSocketCommunicationModule.Options;
using System;
using System.Linq;
// using Jtext103.CFET2.Things.ExampleThing;

namespace Jtext103.CFET2.CFET2App
{
    public partial class Cfet2Program : CFET2Host
    {
        private void AddThings()
        {
            var communication = MyHub.ActiveEventCommunicationConfiguration;
            communication.EnsureStandardHostSupported();
            //------------------------------Pipeline------------------------------//
            MyHub.Pipeline.AddMiddleware(new ResourceInfoMidware());
            MyHub.Pipeline.AddMiddleware(new NavigationMidware());

            //------------------------------HTTP通信模块------------------------------//
            var httpCM = new HTTPCommunicationModule.HTTPCommunicationModule(GlobalConfig.HostUri, GlobalConfig.Accept);
            MyHub.TryAddCommunicationModule(httpCM);



            //you can add Thing by coding here

            //------------------------------Custom View------------------------------//
            var customView = new CustomViewThing();
            MyHub.TryAddThing(customView, "/", "customView", "./CustomView");

            //---------------------Required Event: ws-------------------------------//
            var webSocketOptions = EventCommunicationBootstrap.CreateWebSocketOptions(communication,
                GlobalConfig.IP, int.Parse(GlobalConfig.Port), TimeSpan.FromMilliseconds(GlobalConfig.WebSocketResubscribeIntervalMs));
            ApplyWebSocketEnvironmentOverrides(webSocketOptions);
            EventCommunicationBootstrap.Register(MyHub, webSocketOptions);

            //If you don't want dynamic load things, please comment out the line below
            var loader = new DynamicThingsLoader(this);


            //---------------------------------------Example--------------------------------------------//

            // 如果需要手动挂载，则需要CFET2App先引用/依赖该项目，添加using+namespace之后用如下方式挂载
            // 指定CR的路径，以及配置文件所在的路径
            // 这里配置文件对应的路径是\newcfet2app\CFET2APP\bin\Debug\net10.0\ExampleConfig.json

            // var ExampleThing = new ExampleThing();
            // MyHub.TryAddThing(ExampleThing, "/", "ExampleThing", "./ExampleConfig.json");
        }

        private static void ApplyWebSocketEnvironmentOverrides(WebSocketCommunicationOptions options)
        {
            ApplyPositiveInt(
                "CFET2_WEBSOCKET_MAX_CONCURRENT_FAN_OUT_EVENTS",
                value => options.MaxConcurrentFanOutEvents = value);
            ApplyPositiveInt(
                "CFET2_WEBSOCKET_MAX_FAN_OUT_CONCURRENCY",
                value => options.MaxFanOutConcurrency = value);
        }

        private static void ApplyPositiveInt(string name, Action<int> apply)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            if (int.TryParse(raw, out var value) && value > 0)
            {
                apply(value);
            }
        }
    }
}
