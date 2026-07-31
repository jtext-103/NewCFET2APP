using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace Jtext103.CFET2.CFET2App.cli
{
    public class GotoCommand : CommandBase
    {
        public IList<string> Path { get; set; }

        public static Command Create(CliParser parser)
        {
            var pathArgument = new Argument<string>("path")
            {
                Description = "resource path",
                Arity = ArgumentArity.ExactlyOne
            };
            pathArgument.CompletionSources.Add(parser.GetResourcePathCompletions);

            var command = new Command("goto", "goto a local resource that specified by a path, like goto /thing/status")
            {
                pathArgument
            };

            command.SetAction(parseResult =>
            {
                var gotoCommand = new GotoCommand
                {
                    Path = new List<string> { parseResult.GetRequiredValue(pathArgument) }
                };

                gotoCommand.Execute(parser);
            });

            return command;
        }

        public override void Execute(CliParser parser)
        {
            Setup(parser);
            var path = Path.Single();
            path = GetAbsolutPahtAndQuery(path);
            var resource = MyHub.GetLocalResouce(path);

            if (resource != null)
            {
                MySession.CurrentResource = resource;
                MySession.CurrentPath = path;
                return;
            }
            throw new Exception("Source not found!");
        }
    }
}
