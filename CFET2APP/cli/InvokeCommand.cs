using System;
using System.CommandLine;

namespace Jtext103.CFET2.CFET2App.cli
{
    public class InvokeCommand : GetCommand
    {
        public static Command Create(CliParser parser)
        {
            return CreateResourceCommand("invoke", "invoke a resource sample that specified by a path, more help: invoke --help", parser, () => new InvokeCommand());
        }

        public override void Execute(CliParser parser)
        {
            Setup(parser);
            string resultString = AccessResource(MyHub.TryInvokeSampleResourceWithUri, MyHub.TryInvokeSampleResourceWithUri);
            Console.WriteLine(resultString);
        }
    }
}
