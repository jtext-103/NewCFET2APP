using System;
using System.CommandLine;

namespace Jtext103.CFET2.CFET2App.cli
{
    public class SetCommand : GetCommand
    {
        public static new Command Create(CliParser parser)
        {
            return CreateResourceCommand("set", "set a resource sample that specified by a path, more help: set --help", parser, () => new SetCommand());
        }

        public override void Execute(CliParser parser)
        {
            Setup(parser);
            string resultString = AccessResource(MyHub.TrySetResourceSampleWithUri, MyHub.TrySetResourceSampleWithUri);
            Console.WriteLine(resultString);
        }
    }
}
