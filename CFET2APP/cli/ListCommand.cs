using Jtext103.CFET2.Core.Sample;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace Jtext103.CFET2.CFET2App.cli
{
    public class ListCommand : CommandBase
    {
        public bool IsThings { get; set; }
        public bool IsDetail { get; set; }
        public bool IsAll { get; set; }
        public bool IsChild { get; set; }

        public static Command Create(CliParser parser)
        {
            var isThingsOption = new Option<bool>("--thing", "-t", "--things")
            {
                Description = "list only thing resources"
            };
            var isDetailOption = new Option<bool>("--detail", "-d")
            {
                Description = "show resource type"
            };
            var isAllOption = new Option<bool>("--all", "-a")
            {
                Description = "list all resources"
            };
            var isChildOption = new Option<bool>("--child", "-c")
            {
                Description = "list direct children of current resource"
            };

            var command = new Command("list", "list resources, more help: list --help")
            {
                isThingsOption,
                isDetailOption,
                isAllOption,
                isChildOption
            };

            command.SetAction(parseResult =>
            {
                var listCommand = new ListCommand
                {
                    IsThings = parseResult.GetValue(isThingsOption),
                    IsDetail = parseResult.GetValue(isDetailOption),
                    IsAll = parseResult.GetValue(isAllOption),
                    IsChild = parseResult.GetValue(isChildOption)
                };

                listCommand.Execute(parser);
            });

            return command;
        }

        public override void Execute(CliParser parser)
        {
            Setup(parser);
            var resourceList = MyHub.GetAllLocalResources();
            var resultStrings = new List<string>();

            if (!IsAll)
            {
                if (MySession.CurrentResource == null)
                {
                    throw new Exception("currently not on a legal resource");
                }
                if (IsChild)
                {
                    foreach (var resource in resourceList)
                    {
                        if (resource.Key.ToLower() != MySession.CurrentPath.ToLower()
                            && resource.Key.ToLower().StartsWith(MySession.CurrentPath.ToLower()))
                        {
                            if (resource.Key.Remove(0, MySession.CurrentPath.Length + 1).Contains(@"/") == false)
                            {
                                resultStrings.Add(resource.Key);
                            }
                        }
                    }
                }
                else
                {
                    resultStrings.Add(MySession.CurrentPath);
                }
            }
            else
            {
                resultStrings.AddRange(resourceList.Select(r => r.Key));
            }

            if (IsThings)
            {
                var toRemove = new List<string>();
                foreach (string result in resultStrings)
                {
                    if (MyHub.GetLocalResouce(result).ResourceType != ResourceTypes.Thing)
                    {
                        toRemove.Add(result);
                    }
                }
                foreach (var item in toRemove)
                {
                    resultStrings.Remove(item);
                }
            }

            if (IsDetail)
            {
                for (int i = 0; i < resultStrings.Count; i++)
                {
                    resultStrings[i] = resultStrings[i] + "\t" +
                        Enum.GetName(typeof(ResourceTypes), MyHub.GetLocalResouce(resultStrings[i]).ResourceType);
                }
            }

            for (int i = 0; i < resultStrings.Count; i++)
            {
                Console.WriteLine(resultStrings[i]);
            }
            if (resultStrings.Count == 0)
            {
                Console.WriteLine("No thing found!");
            }
        }
    }
}
