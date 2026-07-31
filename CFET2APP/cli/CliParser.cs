using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Resource;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;

namespace Jtext103.CFET2.CFET2App.cli
{
    public class CliParser
    {
        public CFET2Host Host { get; set; }

        public CliSession MySesstion { get; set; } = new CliSession();

        private readonly RootCommand _rootCommand;
        private readonly InvocationConfiguration _invocationConfiguration = new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false
        };

        public CliParser(CFET2Host host)
        {
            Host = host;
            MySesstion.CurrentPath = @"/";
            try
            {
                MySesstion.CurrentResource = host.MyHub.GetLocalResouce("/");
            }
            catch
            {
                //does nothing
            }

            _rootCommand = CreateRootCommand();
        }

        public void Execute(string command)
        {
            var args = CommandLineParser.SplitCommandLine(command).ToArray();
            var result = _rootCommand.Parse(args);
            if (result.Errors.Count > 0)
            {
                foreach (var error in result.Errors)
                {
                    Console.WriteLine(error.Message);
                }
                Console.WriteLine("Wrong command, please read the help:list -h");
                return;
            }

            try
            {
                result.Invoke(_invocationConfiguration);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public IEnumerable<CompletionItem> GetCompletionItems(string command, int? cursorPosition = null)
        {
            return _rootCommand
                .Parse(command)
                .GetCompletions(cursorPosition);
        }

        internal IEnumerable<CompletionItem> GetResourcePathCompletions(CompletionContext context)
        {
            return GetResourcePathCompletions(context.WordToComplete, null);
        }

        internal IEnumerable<CompletionItem> GetResourcePathCompletions(CompletionContext context, AccessAction action)
        {
            return GetResourcePathCompletions(context.WordToComplete, action);
        }

        private IEnumerable<CompletionItem> GetResourcePathCompletions(string wordToComplete, AccessAction? action)
        {
            var resources = Host?.MyHub?.GetAllLocalResources();
            if (resources == null)
            {
                return Enumerable.Empty<CompletionItem>();
            }

            wordToComplete = wordToComplete ?? string.Empty;
            var completionResources = resources
                .Where(resource => action == null || ResourceSupportsAction(resource.Value, action.Value))
                .OrderBy(resource => resource.Key)
                .ToList();

            if (wordToComplete.StartsWith("/", StringComparison.Ordinal))
            {
                return completionResources
                    .Where(resource => resource.Key.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
                    .Select(CreateResourceCompletionItem);
            }

            var currentPath = MySesstion.CurrentPath ?? "/";
            var currentPathAsDirectory = currentPath.EndsWith("/", StringComparison.Ordinal) ? currentPath : currentPath + "/";
            var baseUri = new Uri(CommonConstants.LocalBaseUri, currentPathAsDirectory);
            var targetUri = new Uri(baseUri, wordToComplete);
            var absolutePrefix = targetUri.PathAndQuery;

            return completionResources
                .Where(resource =>
                    resource.Key.StartsWith(absolutePrefix, StringComparison.OrdinalIgnoreCase) ||
                    resource.Key.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
                .Select(CreateResourceCompletionItem);
        }

        private static bool ResourceSupportsAction(ResourceBase resource, AccessAction action)
        {
            try
            {
                return resource?.Info?.Implementations?.TryGetValue(action, out var implementation) == true
                    && implementation != null;
            }
            catch
            {
                return false;
            }
        }

        private static string GetCompletionInsertText(CompletionItem completion)
        {
            return string.IsNullOrEmpty(completion.InsertText) ? completion.Label : completion.InsertText;
        }

        private static CompletionItem CreateResourceCompletionItem(KeyValuePair<string, ResourceBase> resource)
        {
            var resourceType = resource.Value?.ResourceType.ToString() ?? "Unknown";
            return new CompletionItem(resource.Key, detail: resourceType, insertText: resource.Key);
        }

        private RootCommand CreateRootCommand()
        {
            var rootCommand = new RootCommand("CFET2 command line");
            rootCommand.Subcommands.Add(ListCommand.Create(this));
            rootCommand.Subcommands.Add(GotoCommand.Create(this));
            rootCommand.Subcommands.Add(GetCommand.Create(this));
            rootCommand.Subcommands.Add(SetCommand.Create(this));
            rootCommand.Subcommands.Add(InvokeCommand.Create(this));
            rootCommand.Subcommands.Add(ExitCommand.Create(this));
            return rootCommand;
        }
    }
}
