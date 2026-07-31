using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CommandLine;
using System.CommandLine.Parsing;
namespace Jtext103.CFET2.CFET2App.cli
{
    public class Options
    {
        public Command ListCommand = new Command("list", "list resources, more help: list --help");
        public Command GotoCommand = new Command("goto", "goto a local resource that specified by a path, like goto /thing/status");
        public Command GetCommand = new Command("get", "get a resource sample that specified by a path, more help: get --help");
        public Command SetCommand = new Command("set", "set a resource sample that specified by a path, more help: set --help");
        public Command InvokeCommand = new Command("invoke", "invoke a resource sample that specified by a path, more help: invoke --help");
        public Command ExitCommand = new Command("exit", "Just dispose all things and exit the app");
    }
}
