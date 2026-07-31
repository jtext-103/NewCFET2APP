using System.CommandLine;

namespace Jtext103.CFET2.CFET2App.cli
{
    public class ExitCommand : CommandBase
    {
        public static Command Create(CliParser parser)
        {
            var command = new Command("exit", "Just dispose all things and exit the app");
            command.SetAction(_ => new ExitCommand().Execute(parser));
            return command;
        }

        public override void Execute(CliParser parser)
        {
            Setup(parser);
            MyHub.DisposeThings();
            MySession.ShouldExit = true;
        }
    }
}
