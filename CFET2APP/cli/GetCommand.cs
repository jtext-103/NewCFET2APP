using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Sample;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace Jtext103.CFET2.CFET2App.cli
{
    public class GetCommand : CommandBase
    {
        public IEnumerable<string> InputParams { get; set; }

        public string InputDict { get; set; }

        public bool IsSample { get; set; }

        public bool IsJson { get; set; }

        public IList<string> Path { get; set; }

        public static Command Create(CliParser parser)
        {
            return CreateResourceCommand("get", "get a resource sample that specified by a path, more help: get --help", parser, () => new GetCommand());
        }

        protected static Command CreateResourceCommand<TCommand>(string name, string description, CliParser parser, Func<TCommand> commandFactory)
            where TCommand : GetCommand
        {
            var inputParamsOption = new Option<string[]>("--params", "-p")
            {
                Description = "the input parameters for this",
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true,
                DefaultValueFactory = _ => Array.Empty<string>()
            };
            var inputDictOption = new Option<string>("--dict", "-d")
            {
                Description = "the input dictionary for the resource, input must in json form"
            };
            var isSampleOption = new Option<bool>("--sample", "-s")
            {
                Description = "show the whole sample instead of just value"
            };
            var isJsonOption = new Option<bool>("--json", "-j")
            {
                Description = "indicate the input parameters are json string"
            };
            var pathArgument = new Argument<string>("path")
            {
                Description = "resource path",
                Arity = ArgumentArity.ZeroOrOne
            };
            pathArgument.CompletionSources.Add(context => parser.GetResourcePathCompletions(context, GetAccessAction(name)));

            var command = new Command(name, description)
            {
                inputParamsOption,
                inputDictOption,
                isSampleOption,
                isJsonOption,
                pathArgument
            };

            command.Validators.Add(result =>
            {
                var inputParams = result.GetValue(inputParamsOption);
                var inputDict = result.GetValue(inputDictOption);
                if (inputParams != null && inputParams.Length > 0 && !string.IsNullOrEmpty(inputDict))
                {
                    result.AddError("--params and --dict cannot be used together.");
                }
            });

            command.SetAction(parseResult =>
            {
                var path = parseResult.GetValue(pathArgument);
                var commandOption = commandFactory();
                commandOption.InputParams = parseResult.GetValue(inputParamsOption) ?? Array.Empty<string>();
                commandOption.InputDict = parseResult.GetValue(inputDictOption);
                commandOption.IsSample = parseResult.GetValue(isSampleOption);
                commandOption.IsJson = parseResult.GetValue(isJsonOption);
                commandOption.Path = string.IsNullOrEmpty(path) ? new List<string>() : new List<string> { path };
                commandOption.Execute(parser);
            });

            return command;
        }

        private static AccessAction GetAccessAction(string commandName)
        {
            switch (commandName)
            {
                case "set":
                    return AccessAction.set;
                case "invoke":
                    return AccessAction.invoke;
                default:
                    return AccessAction.get;
            }
        }

        public override void Execute(CliParser parser)
        {
            Setup(parser);
            string resultString = AccessResource(MyHub.TryGetResourceSampleWithUri, MyHub.TryGetResourceSampleWithUri);
            Console.WriteLine(resultString);
        }

        public string AccessResource(Func<string, object[], ISample> arrayAccessFunc, Func<string, Dictionary<string, object>, ISample> dictAccessFunc)
        {
            var path = MySession.CurrentPath;
            if (Path.Count == 1)
            {
                path = Path.Single();
            }

            if (!(new Uri(path, UriKind.RelativeOrAbsolute)).IsAbsoluteUri) //if not a absolute uri than it's a local path try to make a full path, it can be a relative path
            {
                path = GetAbsolutPahtAndQuery(path);
            }
            var realInputs = new object[] { };
            Dictionary<string, object> realInputDict = null;
            if (InputParams != null && InputParams.Count() != 0)
            {
                realInputs = makeInputs(InputParams, IsJson);
            }
            if (!(string.IsNullOrEmpty(InputDict))) //we have dictinary input
            {
                realInputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(InputDict);
            }
            ISample result = null;
            if (realInputDict != null)
            {
                //result = MyHub.TryGetResourceSampleWithUri(path, realInputDict);
                result = dictAccessFunc(path, realInputDict);
            }
            else
            {
                //result = MyHub.TryGetResourceSampleWithUri(path, realInputs);
                result = arrayAccessFunc(path, realInputs);
            }

            var resultString = result.ToString();
            if (IsSample)
            {
                resultString = JsonConvert.SerializeObject(result, Formatting.Indented);
            }

            return resultString;
        }

        private object[] makeInputs(IEnumerable<string> inputParams, bool isJson)
        {
            if (!IsJson)
            {
                return inputParams.Cast<object>().ToArray();
            }
            var newInputs = new List<object>();
            var inputArray = inputParams.ToArray();
            for (int i = 0; i < inputArray.Length; i++)
            {
                newInputs.Add(JsonConvert.DeserializeObject(inputArray[i]));
            }
            return newInputs.ToArray();
        }
    }
}
