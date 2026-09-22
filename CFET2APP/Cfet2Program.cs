using Jtext103.CFET2.CFET2App.cli;
using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Config;
using Jtext103.CFET2.Core.Log;
using System;

namespace Jtext103.CFET2.CFET2App
{
    partial class Cfet2Program : CFET2Host
    {
        static void Main(string[] args)
        {
            //first thing: init LogManager
            NlogProvider.Config();
            Cfet2LogManager.SetLogProvider(new NlogProvider());

            // Load settings before Hub/EventHub construction captures the observation interval.
            GlobalConfig.Populate("./GlobalConfig.json");

            //inject and init the host app module
            var host = new Cfet2Program();
            HubMaster.InjectHubToModule(host);

            //add things, which defined in xxPartial.cs
            host.AddThings();

            // Start cleanup after configuration and component registration.
            Cfet2LogManager.StartLogCleanup();

            //start communication modules
            host.MyHub.StartCommunication();

            //start all thins
            host.MyHub.StartThings();

            if (Array.Exists(args, arg => string.Equals(arg, "--no-cli", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("Cfet2 host running without CLI");
                System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
                return;
            }

            //start cli loop 
            var cli = new CliParser(host);
            cli.Host = host;
            Console.WriteLine("Cfet2 host Cli started");
            ReadLine.HistoryEnabled = false;
            ReadLine.AutoCompletionHandler = new CliCompletionHandler(cli);
            while (true)
            {
                var command = ReadLine.Read("Cfet2> ");
                if (string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }
                try
                {
                    cli.Execute(command);
                    ReadLine.AddHistory(command);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                if (cli.MySesstion.ShouldExit)
                {
                    //quit the app
                    break;
                }
            }

        }
    }
}
