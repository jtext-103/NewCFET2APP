# newcfet2app 新命令行交互功能

## 1. 功能概述

newcfet2app 提供了交互式命令行，用于查看、定位、读取、设置和调用 CFET2 本地资源。

新命令行交互功能由以下两个 NuGet 包共同实现：

- `ReadLine 2.0.1`：负责命令行输入、行内编辑、上下方向键历史记录和 Tab 键交互。
- `System.CommandLine 2.0.8`：负责定义命令、参数和选项，以及命令解析、校验、执行和候选项生成。

两者的职责关系如下：

```text
ReadLine
    负责读取用户输入和响应 Tab 键
        ↓
CliCompletionHandler
    将 ReadLine 的补全请求转发给 CliParser
        ↓
System.CommandLine
    根据当前输入生成命令、选项和资源路径候选项
```

新命令行支持以下交互功能：

- 使用左右方向键移动光标并编辑当前输入。
- 使用上下方向键浏览本次运行期间执行过的命令。
- 使用 Tab 键补全命令名称、命令选项和资源路径。
- 根据当前路径补全相对资源路径。
- 根据 `get`、`set`、`invoke` 命令需要的操作能力过滤资源候选项。

## 2. 命令行使用方法

### 2.1 启动与退出

启动 newcfet2app 后，程序完成 Host、Thing 和通信模块初始化，然后进入命令行交互循环：

```text
Cfet2 host Cli started
Cfet2>
```

在 `Cfet2>` 提示符后输入命令并按 Enter 执行。

使用以下命令退出程序：

```text
Cfet2> exit
```

`exit` 会释放已启动的 Thing，并通知命令行循环结束运行。

### 2.2 基本命令

当前命令行支持以下命令：

| 命令 | 作用 | 示例 |
| --- | --- | --- |
| `list` | 列出当前资源或资源列表 | `list --all` |
| `goto` | 将当前会话路径切换到指定本地资源 | `goto /ExampleThing` |
| `get` | 读取资源数据 | `get /ExampleThing/status` |
| `set` | 设置资源数据 | `set /ExampleThing/config --params value` |
| `invoke` | 调用可执行资源 | `invoke /ExampleThing/action` |
| `exit` | 释放 Thing 并退出程序 | `exit` |

可以使用 `--help` 或 `-h` 查看命令帮助。例如：

```text
Cfet2> list --help
Cfet2> get --help
```

`System.CommandLine` 会根据命令定义自动生成帮助信息。

### 2.3 命令行编辑

输入命令时，可以使用 ReadLine 提供的行内编辑能力：

- 左方向键：将光标向左移动。
- 右方向键：将光标向右移动。
- Backspace：删除光标前的字符。
- Delete：删除光标位置的字符。
- Home：移动到输入行开头。
- End：移动到输入行末尾。

例如，输入命令后可以移动光标到中间位置，修改资源路径或命令选项，无需清空整行重新输入。

### 2.4 上下方向键历史记录

执行过的非空命令会加入 ReadLine 历史记录：

- 按上方向键，查看并重新使用较早执行的命令。
- 按下方向键，返回较新的历史命令。
- 空字符串或仅包含空白字符的输入不会被执行，也不会加入历史记录。

程序关闭了 ReadLine 的自动历史记录：

```csharp
ReadLine.HistoryEnabled = false;
```

命令成功进入 `CliParser.Execute` 调用后，程序会手动添加历史记录：

```csharp
ReadLine.AddHistory(command);
```

手动记录可以避免空命令进入历史记录。当前历史记录只保存在进程内，程序退出后不会持久化到文件。

### 2.5 Tab 自动补全

在命令行中按 Tab 键，可以补全当前正在输入的内容。

#### 补全命令名称

输入命令名称的一部分后按 Tab：

```text
Cfet2> go<Tab>
```

可补全为：

```text
Cfet2> goto
```

根命令下的 `list`、`goto`、`get`、`set`、`invoke` 和 `exit` 都由 `System.CommandLine` 提供命令名称候选项。

#### 补全命令选项

输入命令和部分选项名称后按 Tab：

```text
Cfet2> list --d<Tab>
```

可补全为：

```text
Cfet2> list --detail
```

命令选项来自各命令类中的 `Option<T>` 定义。例如，`list` 支持 `--thing`、`--detail`、`--all` 和 `--child` 等选项。

#### 补全资源路径

需要资源路径的命令支持路径补全。例如：

```text
Cfet2> get /ExampleThing/st<Tab>
```

补全候选项来自当前 Host 中已经加载的本地资源。

资源路径补全支持以下命令：

- `goto`
- `get`
- `set`
- `invoke`

#### 单个候选项

如果只有一个匹配项，按 Tab 后会直接补全该候选项。

#### 多个候选项

如果存在多个匹配项，补全处理器会先计算这些候选项的最长公共前缀。

例如，候选项为：

```text
/ExampleThing/status
/ExampleThing/statusDetail
```

当已输入内容短于公共前缀时，第一次按 Tab 会尽可能补全到公共前缀。无法继续扩展公共前缀时，ReadLine 会使用候选列表进行后续交互。

候选项匹配、去重和排序均不区分大小写。

### 2.6 绝对路径与相对路径补全

资源路径补全同时支持绝对路径和相对路径。

#### 绝对路径

以 `/` 开头的输入被视为绝对资源路径：

```text
Cfet2> get /ExampleThing/st<Tab>
```

补全机制会直接筛选所有以当前输入开头的本地资源。

#### 相对路径

未以 `/` 开头的输入会基于当前会话路径进行解析：

```text
Cfet2> goto /ExampleThing
Cfet2> get st<Tab>
```

执行 `goto` 后，`CliSession.CurrentPath` 会变为 `/ExampleThing`。此时 `st` 会按照 `/ExampleThing/st` 的方向查找补全候选项。

补全阶段和命令执行阶段都使用 URI 方式将相对路径转换为绝对路径，从而保持路径处理规则一致。

### 2.7 按资源操作能力过滤补全结果

不同命令对资源能力有不同要求。补全机制会过滤不支持对应操作的资源，减少无效候选项。

| 命令 | 补全范围 |
| --- | --- |
| `goto` | 所有本地资源 |
| `get` | 支持 `AccessAction.get` 的资源 |
| `set` | 支持 `AccessAction.set` 的资源 |
| `invoke` | 支持 `AccessAction.invoke` 的资源 |

例如，在 `set` 命令后按 Tab，不会推荐只支持读取、不支持设置的资源。

## 3. 整体实现架构

### 3.1 核心组件

新命令行交互功能主要由以下组件实现：

| 组件 | 主要职责 |
| --- | --- |
| `Cfet2Program` | 初始化命令行、配置 ReadLine、运行交互循环 |
| `CliParser` | 创建命令树、解析并执行命令、生成补全项 |
| `CliCompletionHandler` | 将 ReadLine 的 Tab 请求接入 System.CommandLine |
| `CliSession` | 保存当前资源、当前路径和退出状态 |
| 各命令类 | 定义命令参数、选项、执行行为和自定义补全源 |

### 3.2 命令执行流程

命令执行流程如下：

```text
用户输入命令并按 Enter
    ↓
ReadLine.Read("Cfet2> ")
    ↓
忽略空白输入
    ↓
CliParser.Execute(command)
    ↓
CommandLineParser.SplitCommandLine(command)
    ↓
RootCommand.Parse(args)
    ↓
检查解析错误
    ↓
ParseResult.Invoke
    ↓
执行对应命令类
    ↓
ReadLine.AddHistory(command)
```

`CommandLineParser.SplitCommandLine` 负责按照命令行规则拆分输入，能够正确处理参数和带引号的内容。解析完成后，`System.CommandLine` 会执行命令定义中通过 `SetAction` 注册的行为。

### 3.3 Tab 补全流程

Tab 补全流程如下：

```text
用户在 ReadLine 中按 Tab
    ↓
ReadLine 调用 CliCompletionHandler.GetSuggestions
    ↓
CliCompletionHandler 调用 CliParser.GetCompletionItems
    ↓
RootCommand.Parse(command).GetCompletions(cursorPosition)
    ↓
System.CommandLine 生成命令、选项或参数候选项
    ↓
自定义 CompletionSources 生成资源路径候选项
    ↓
CliCompletionHandler 筛选、去重和排序
    ↓
返回单个候选项、最长公共前缀或候选列表
    ↓
ReadLine 更新当前输入或展示候选项
```

## 4. 新 System.CommandLine 的实现

### 4.1 NuGet 依赖

项目在 `CFET2APP.csproj` 中引用以下命令行相关依赖：

```xml
<PackageReference Include="ReadLine" Version="2.0.1" />
<PackageReference Include="System.CommandLine" Version="2.0.8" />
```

新的 `System.CommandLine` API 使用 `RootCommand`、`Command`、`Argument<T>`、`Option<T>` 和 `SetAction` 组织命令行功能。

### 4.2 RootCommand 与命令注册

`CliParser.CreateRootCommand` 创建根命令，并注册所有子命令：

```csharp
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
```

每个命令类通过自己的静态 `Create` 方法创建 `Command`。这种方式使命令定义、参数配置和执行行为集中在对应命令类中。

### 4.3 参数与选项定义

命令位置参数使用 `Argument<T>` 定义。例如，`goto` 命令要求提供一个资源路径：

```csharp
var pathArgument = new Argument<string>("path")
{
    Description = "resource path",
    Arity = ArgumentArity.ExactlyOne
};
```

命令选项使用 `Option<T>` 定义。例如：

```csharp
var isDetailOption = new Option<bool>("--detail", "-d")
{
    Description = "show resource type"
};
```

`Arity` 用于限制参数数量。`get`、`set` 和 `invoke` 的路径参数允许不填写，此时命令会使用当前会话路径。

### 4.4 命令校验与执行

命令可以注册校验规则。例如，资源访问命令不允许同时使用 `--params` 和 `--dict`：

```csharp
command.Validators.Add(result =>
{
    var inputParams = result.GetValue(inputParamsOption);
    var inputDict = result.GetValue(inputDictOption);
    if (inputParams != null && inputParams.Length > 0 &&
        !string.IsNullOrEmpty(inputDict))
    {
        result.AddError("--params and --dict cannot be used together.");
    }
});
```

命令通过 `SetAction` 注册执行行为。执行时从 `ParseResult` 获取参数和选项值，然后创建并执行对应命令对象。

### 4.5 命令解析

`CliParser.Execute` 首先拆分并解析命令：

```csharp
var args = CommandLineParser.SplitCommandLine(command).ToArray();
var result = _rootCommand.Parse(args);
```

如果解析结果包含错误，程序会输出错误信息并停止本次执行。解析成功后，程序使用 `Invoke` 执行对应命令。

`InvocationConfiguration.EnableDefaultExceptionHandler` 被设置为 `false`，命令执行异常由 newcfet2app 自己捕获并输出。

## 5. ReadLine 交互循环的实现

### 5.1 初始化 ReadLine

在进入命令循环前，`Cfet2Program` 创建 `CliParser` 并配置 ReadLine：

```csharp
var cli = new CliParser(host);
cli.Host = host;

ReadLine.HistoryEnabled = false;
ReadLine.AutoCompletionHandler = new CliCompletionHandler(cli);
```

`CliCompletionHandler` 持有同一个 `CliParser` 实例，因此补全时能够访问当前 Host 中的资源和当前 `CliSession` 状态。

### 5.2 输入循环

交互循环通过 `ReadLine.Read` 获取输入：

```csharp
while (true)
{
    var command = ReadLine.Read("Cfet2> ");
    if (string.IsNullOrWhiteSpace(command))
    {
        continue;
    }

    cli.Execute(command);
    ReadLine.AddHistory(command);

    if (cli.MySesstion.ShouldExit)
    {
        break;
    }
}
```

实际代码还包含异常捕获，避免单条命令执行失败后终止整个命令行程序。

## 6. 补全机制的具体实现

### 6.1 ReadLine 与 System.CommandLine 的连接

`CliCompletionHandler` 实现 ReadLine 提供的 `IAutoCompleteHandler`：

```csharp
internal class CliCompletionHandler : IAutoCompleteHandler
```

处理器通过以下属性指定空格为单词分隔符：

```csharp
public char[] Separators { get; set; } = new[] { ' ' };
```

用户按 Tab 后，ReadLine 调用 `GetSuggestions`。该方法再调用：

```csharp
cli.GetCompletionItems(text, text.Length)
```

由此将 ReadLine 的交互输入接入新的 `System.CommandLine` 补全机制。

### 6.2 System.CommandLine 候选项生成

`CliParser.GetCompletionItems` 使用当前输入创建解析结果，并获取光标位置对应的候选项：

```csharp
public IEnumerable<CompletionItem> GetCompletionItems(
    string command,
    int? cursorPosition = null)
{
    return _rootCommand
        .Parse(command)
        .GetCompletions(cursorPosition);
}
```

`System.CommandLine` 可以自动生成以下候选项：

- 根命令下的子命令名称。
- 当前命令可用的选项名称。
- 参数注册的自定义补全项。

### 6.3 候选项整理

`CliCompletionHandler.GetSuggestions` 将 `CompletionItem` 转换为 ReadLine 使用的字符串：

- 优先使用 `CompletionItem.InsertText`。
- `InsertText` 为空时使用 `CompletionItem.Label`。
- 根据当前单词筛选候选项。
- 忽略大小写去重。
- 忽略大小写排序。

如果没有匹配项，返回 `null`。

如果只有一个匹配项，直接返回该候选项，让 ReadLine 完成插入。

如果有多个匹配项，则计算最长公共前缀：

```text
多个候选项
    ↓
存在比当前输入更长的公共前缀
    → 返回公共前缀，执行部分补全

不存在更长的公共前缀
    → 返回全部候选项，交给 ReadLine 展示或选择
```

### 6.4 注册资源路径补全源

`goto` 命令在路径参数上注册全部资源补全：

```csharp
pathArgument.CompletionSources.Add(parser.GetResourcePathCompletions);
```

`get`、`set` 和 `invoke` 共享资源命令创建逻辑，并按照操作类型注册补全：

```csharp
pathArgument.CompletionSources.Add(
    context => parser.GetResourcePathCompletions(
        context,
        GetAccessAction(name)));
```

当 `System.CommandLine` 判断当前需要补全路径参数时，会调用对应的 `CompletionSources`。

### 6.5 获取资源路径候选项

资源补全首先获取所有本地资源：

```csharp
var resources = Host?.MyHub?.GetAllLocalResources();
```

如果 Host、Hub 或资源列表不可用，则返回空候选集合，避免补全操作导致命令行异常。

对于绝对路径，补全逻辑直接查找路径前缀匹配的资源：

```csharp
resource.Key.StartsWith(
    wordToComplete,
    StringComparison.OrdinalIgnoreCase)
```

对于相对路径，补全逻辑根据 `CliSession.CurrentPath` 构建基础 URI，再将用户输入转换为绝对路径前缀。

每个匹配资源会转换为 `CompletionItem`：

```csharp
new CompletionItem(
    resource.Key,
    detail: resourceType,
    insertText: resource.Key);
```

其中：

- `Label` 是资源路径。
- `InsertText` 是最终插入命令行的资源路径。
- `Detail` 是资源类型。

当前实现最终插入的是资源绝对路径。

### 6.6 根据资源能力过滤

`get`、`set` 和 `invoke` 的候选项会根据资源实现的操作能力过滤：

```csharp
resource.Info.Implementations.TryGetValue(
    action,
    out var implementation)
```

只有存在对应实现且实现对象不为空的资源，才会成为候选项。

能力检查包含异常保护。当资源信息不完整或读取能力信息失败时，该资源不会加入候选列表，也不会中断命令行补全。

## 7. 扩展新的补全功能

### 7.1 添加新命令

添加新命令时，可以按照现有命令类的结构实现：

1. 创建新的命令类。
2. 在静态 `Create(CliParser parser)` 方法中创建 `Command`。
3. 定义命令参数和选项。
4. 使用 `SetAction` 注册执行逻辑。
5. 在 `CliParser.CreateRootCommand` 中注册新命令。

命令注册到 `RootCommand` 后，命令名称和选项名称会自动进入 `System.CommandLine` 补全体系。

### 7.2 为参数添加自定义补全

为参数创建补全函数后，将其添加到参数的 `CompletionSources`：

```csharp
var argument = new Argument<string>("name");
argument.CompletionSources.Add(context =>
{
    return GetCustomCompletions(context.WordToComplete);
});
```

补全函数应返回 `IEnumerable<CompletionItem>`，并尽量根据 `WordToComplete` 提前过滤候选项。

### 7.3 添加新的资源操作补全

如果新增命令只允许操作具有特定能力的资源，可以复用资源补全机制：

1. 为新命令确定对应的 `AccessAction`。
2. 调用带 `AccessAction` 参数的 `GetResourcePathCompletions`。
3. 确保资源的 `Info.Implementations` 中正确注册对应实现。

这样可以保证补全结果与命令实际可操作的资源范围一致。

## 8. 当前行为与注意事项

- 历史记录仅在当前程序运行期间有效，不会持久化到文件。
- 空白输入不会执行，也不会加入历史记录。
- 命令执行进入 `CliParser.Execute` 后会被加入历史记录，即使命令解析或执行结果包含错误。
- 补全候选项来自当前 Host 已加载的本地资源，资源变化后再次按 Tab 会重新读取资源列表。
- 相对路径补全依赖 `CliSession.CurrentPath`，执行 `goto` 会改变后续相对路径的补全范围。
- `goto` 补全所有本地资源；`get`、`set` 和 `invoke` 会按照资源能力过滤。
- ReadLine 补全处理器当前只使用空格作为单词分隔符。
- 当前补全调用使用输入文本长度作为光标位置，主要面向在输入末尾按 Tab 的场景。
- 资源路径候选项最终插入绝对路径，而不是用户输入形式的相对路径。
- 候选项筛选、去重和排序不区分大小写。

## 9. 相关代码文件

| 文件 | 作用 |
| --- | --- |
| `CFET2APP/Cfet2Program.cs` | ReadLine 初始化和交互循环 |
| `CFET2APP/cli/CliParser.cs` | 命令树、命令执行和资源补全实现 |
| `CFET2APP/cli/CliCompletionHandler.cs` | ReadLine 与 System.CommandLine 的补全适配 |
| `CFET2APP/cli/CliSession.cs` | 当前资源路径和退出状态 |
| `CFET2APP/cli/CommandBase.cs` | 命令公共能力和相对路径转换 |
| `CFET2APP/cli/ListCommand.cs` | `list` 命令定义 |
| `CFET2APP/cli/GotoCommand.cs` | `goto` 命令和全部资源补全注册 |
| `CFET2APP/cli/GetCommand.cs` | 资源访问命令公共定义和按能力补全注册 |
| `CFET2APP/cli/SetCommand.cs` | `set` 命令定义 |
| `CFET2APP/cli/InvokeCommand.cs` | `invoke` 命令定义 |
| `CFET2APP/cli/ExitCommand.cs` | `exit` 命令定义 |
| `CFET2APP/CFET2APP.csproj` | ReadLine 和 System.CommandLine 依赖配置 |
