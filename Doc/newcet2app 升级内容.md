# newcfet2app 升级内容

## 本轮实现补充（2026-09-21）

- 新增 `--no-cli`，在通信模块和 Thing 启动后持续运行，不进入 CLI。
- `GlobalConfig.json` 在 Hub 创建前加载，确保 `ResourceObservationIntervalMs` 生效。
- APP 通过 `WebSocketCommunicationOptions` 配置模块，传入 `WebSocketResubscribeIntervalMs`（默认 10 ms，允许 0）。
- 支持 `CFET2_WEBSOCKET_MAX_CONCURRENT_FAN_OUT_EVENTS` 和 `CFET2_WEBSOCKET_MAX_FAN_OUT_CONCURRENCY` 两个正整数环境变量。
- 默认挂载 `/WebSocketCM`，提供连接和订阅诊断。
- ExampleThing 使用资源订阅与 `PublishResource(GetPathFor(nameof(...)), value)`；完整的自动观测和主动发布示例见 [Thing 开发指南](../HowToMakeCfetThings.md#事件与资源订阅)。
- 运行配置和日志边界以 [README](../README.md) 为准。

## 1. 文档说明

本文档整理当前 `newcfet2app` 相比旧版 `jtext103_cfet2app` 的主要升级内容，包括运行框架、通信模块、控制台交互、动态 DLL 加载、NuGet 依赖和跨平台能力等方面。

当前已移除跨仓库源码项目引用，恢复为固定版本 NuGet 引用，等待按 Core → HTTP/WebSocket → APP 顺序逐个发布并更新依赖。Core 引用为 3.1.0，HTTP/WebSocket 引用均已升级为已发布的 NuGet 1.0.1。详见 [提交与依赖交付说明](提交与依赖交付说明.md)。

## 2. 升级内容概览

本次升级的主要内容如下：

- CFET2APP 主程序从 `net6.0` 和 `net461` 多目标框架升级为 `net10.0`。
- 应用版本从 `1.0.2` 升级为 `2.0.0`。
- HTTP 通信模块从 Nancy HTTP 升级为新的 HTTPCommunicationModule。
- WebSocket 远程事件能力从特殊 Thing 模式升级为标准通信模块。
- 控制台命令解析从旧版 CommandLineParser 升级为 System.CommandLine。
- 引入 ReadLine，实现行内编辑、命令历史和 Tab 自动补全。
- 动态 DLL 加载逻辑适配新版 Autofac。
- 升级主要 NuGet 包，并移除旧框架兼容包和 Nancy 相关依赖。
- 内存状态获取方式升级为跨平台实现。
- 发布脚本和发布配置升级到 `net10.0`。
- 新增 `appsettings.json` 日志配置。

## 3. .NET 运行框架升级

### 3.1 CFET2APP 主程序

旧版 CFET2APP 同时支持：

```xml
<TargetFrameworks>net6.0;net461</TargetFrameworks>
<Version>1.0.2</Version>
```

当前 CFET2APP 使用：

```xml
<TargetFramework>net10.0</TargetFramework>
<Version>2.0.0</Version>
```

升级后的变化包括：

- CFET2APP 主程序统一运行于 `.NET 10`。
- 不再生成 `net461` 和 `net6.0` 两套主程序。
- 删除 `.NET Framework 4.6.1` 使用的 Binding Redirect 配置。
- 删除旧版 `App.config` 和 `App.net461.config`。
- 可以直接使用现代 .NET 和 ASP.NET Core 提供的运行能力。
- 简化构建、依赖管理和发布目录结构。

### 3.2 Thing 与 Core 的目标框架

当前 CFET2APP 主程序和新通信模块已经升级为 `net10.0`。

CFET2Core 和 ExampleThing 仍保持 `netstandard2.0`，用于维持 Core 和 Thing DLL 的通用性与兼容性。因此，“升级为 .NET 10”主要指 CFET2APP 主程序及其运行和通信环境。

## 4. HTTP 通信模块升级

旧版使用 `NancyHttpCommunicationModule` 提供 HTTP 通信能力：

```csharp
var nancyCM = new NancyCommunicationModule(
    GlobalConfig.HostUri,
    GlobalConfig.Accept);
MyHub.TryAddCommunicationModule(nancyCM);
```

当前版本使用新的 `HTTPCommunicationModule`：

```csharp
var httpCM = new HTTPCommunicationModule.HTTPCommunicationModule(
    GlobalConfig.HostUri,
    GlobalConfig.Accept);
MyHub.TryAddCommunicationModule(httpCM);
```

主要变化：

- 移除 Nancy HTTP 框架。
- 移除 Nancy Razor 和旧版 ASP.NET Web API Client 相关依赖。
- 新 HTTP 模块基于现代 ASP.NET Core 运行环境。
- 更适合运行于 `.NET 10`、Linux 和容器环境。
- 降低旧版 Nancy 依赖带来的兼容和维护成本。

## 5. WebSocket 通信模块升级

旧版远程事件能力通过 `WebsocketEventThing` 实现。该对象作为 Thing 挂载到 CFET2APP，并手动加入远程事件 Hub：

```csharp
var remoteHub = new WebsocketEventThing();
MyHub.TryAddThing(remoteHub, "/", "WsEvent", "");
MyHub.EventHub.RemoteEventHubs.Add(remoteHub);
```

当前版本将 WebSocket 作为标准通信模块注册：

```csharp
Uri webSocketUri =
    new Uri(GlobalConfig.IP + ":" + (int.Parse(GlobalConfig.Port) + 1));

var remoteHub =
    new WebSocketCommunicationModule.WebSocketCommunicationModule(
        webSocketUri);

MyHub.TryAddCommunicationModule(remoteHub);
```

升级后的变化：

- WebSocket 不再作为特殊 Thing 挂载。
- 远程事件通信统一纳入 CommunicationModule 生命周期管理。
- 不再需要手动向 `RemoteEventHubs` 注册对象。
- 动态 Thing 加载器不再负责识别和挂载 `IRemoteEventHub`。
- HTTP 和 WebSocket 通信模块采用更统一的启动和管理方式。

## 6. 控制台交互升级

### 6.1 控制台输入方式

旧版控制台使用 `Console.ReadLine()` 获取用户输入：

```csharp
Console.Write("Cfet2> ");
var command = Console.ReadLine();
```

当前版本使用 ReadLine：

```csharp
ReadLine.HistoryEnabled = false;
ReadLine.AutoCompletionHandler = new CliCompletionHandler(cli);

var command = ReadLine.Read("Cfet2> ");
```

新增的控制台交互能力：

- 使用左右方向键移动光标并编辑当前命令。
- 使用 Home、End、Backspace 和 Delete 编辑输入内容。
- 使用上下方向键浏览本次运行期间的命令历史。
- 忽略空字符串和仅包含空白字符的命令。
- 使用 Tab 键补全命令、选项和资源路径。

ReadLine 的自动历史记录被关闭，CFET2APP 会在命令进入执行流程后手动记录：

```csharp
ReadLine.AddHistory(command);
```

当前历史记录仅在本次程序运行期间有效，不会持久化到文件。

### 6.2 命令解析框架

旧版命令行使用 `CommandLineParser 1.9.71`，通过属性标记定义命令和选项：

```csharp
[VerbOption("list")]
[Option('a', "all")]
```

当前版本使用 `System.CommandLine 2.0.8`，通过命令树定义命令：

```csharp
var rootCommand = new RootCommand("CFET2 command line");
rootCommand.Subcommands.Add(ListCommand.Create(this));
rootCommand.Subcommands.Add(GotoCommand.Create(this));
rootCommand.Subcommands.Add(GetCommand.Create(this));
rootCommand.Subcommands.Add(SetCommand.Create(this));
rootCommand.Subcommands.Add(InvokeCommand.Create(this));
rootCommand.Subcommands.Add(ExitCommand.Create(this));
```

升级后的变化：

- 命令、参数和选项拥有统一的结构化定义。
- 自动生成命令帮助信息。
- 支持参数数量校验。
- 支持自定义命令校验规则。
- 支持正确拆分带引号的命令参数。
- 命令解析错误可以输出更明确的错误信息。
- 命令执行异常由 CFET2APP 自己捕获，单条命令失败不会结束控制台程序。

当前保留以下主要命令：

| 命令 | 功能 |
| --- | --- |
| `list` | 查看当前资源、子资源或全部资源 |
| `goto` | 切换当前会话资源路径 |
| `get` | 获取资源数据 |
| `set` | 设置资源数据 |
| `invoke` | 调用可执行资源 |
| `exit` | 释放 Thing 并退出程序 |

### 6.3 Tab 自动补全

当前版本新增 `CliCompletionHandler`，用于连接 ReadLine 和 System.CommandLine 的补全能力。

补全流程如下：

```text
用户按下 Tab
    ↓
ReadLine 调用 CliCompletionHandler
    ↓
CliCompletionHandler 请求 CliParser 获取候选项
    ↓
System.CommandLine 根据当前输入生成候选项
    ↓
ReadLine 插入候选项或展示候选列表
```

支持补全的内容包括：

- 命令名称，例如将 `go` 补全为 `goto`。
- 命令选项，例如将 `list --d` 补全为 `list --detail`。
- 本地资源绝对路径。
- 基于当前 `goto` 路径计算的相对资源路径。

当存在多个候选项时，补全处理器会优先计算并补全最长公共前缀；无法继续扩展公共前缀时，再展示候选列表。

### 6.4 按资源能力过滤补全结果

资源路径补全会根据不同命令要求的操作能力进行过滤：

| 命令 | 补全范围 |
| --- | --- |
| `goto` | 所有本地资源 |
| `get` | 支持 `get` 操作的资源 |
| `set` | 支持 `set` 操作的资源 |
| `invoke` | 支持 `invoke` 操作的资源 |

该功能可以减少无效资源候选项，使补全结果更符合命令的实际可操作范围。

## 7. 动态 DLL 加载升级

### 7.1 保留的动态加载流程

当前版本继续支持通过 `thingConfig` 和 `thingDll` 动态挂载 Thing：

1. 递归扫描 `thingConfig` 目录。
2. 读取 Thing 的 `Config.json`。
3. 将 `thingDll` 和 Thing 配置目录下的依赖 DLL 复制到运行目录。
4. 扫描运行目录中的 DLL。
5. 使用 Autofac 创建 Thing 实例。
6. 根据配置将 Thing 挂载到 CFET2APP。

### 7.2 适配 Autofac 9

旧版使用 Autofac 5，直接注册程序集中的全部类型：

```csharp
builder.RegisterAssemblyTypes(assembly);
builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();
```

当前版本升级到 Autofac 9。新版 Autofac 对无法实例化的类型注册更加严格，因此动态加载器会先过滤类型：

```csharp
var types = assembly.GetTypes()
    .Where(t => t.IsClass
        && !t.IsAbstract
        && t.GetConstructors().Any(c => c.IsPublic))
    .ToArray();

builder.RegisterTypes(types);
builder.RegisterTypes(types).AsImplementedInterfaces();
```

当前只会向 Autofac 注册：

- 具体类。
- 非抽象类。
- 至少具有一个公共构造函数的类。

该调整避免接口、抽象类、内部类型和无公共构造函数类型导致 Autofac 容器构建失败。

### 7.3 远程事件挂载职责调整

旧版动态加载器会判断动态创建的实例是否实现 `IRemoteEventHub`，并自动加入远程事件 Hub。

当前版本已经移除该逻辑。远程事件通信统一由新的 WebSocketCommunicationModule 负责，不再由普通动态 Thing 承担。

### 7.4 当前动态加载能力边界

当前版本实现的是程序启动阶段的动态发现和挂载，并未实现运行期间的 DLL 热更新。

当前仍使用默认程序集加载上下文和 `Assembly.LoadFrom`，因此存在以下限制：

- 已加载 DLL 无法在程序运行期间安全卸载。
- 运行期间替换已加载 DLL 可能失败。
- 不同 Thing 使用同名但不同版本依赖时，可能发生版本冲突。
- DLL 复制或加载失败时，部分异常只会记录简单提示。

## 8. NuGet 包升级

### 8.1 升级的主要 NuGet 包

| NuGet 包 | 旧版版本 | 当前版本 | 主要影响 |
| --- | ---: | ---: | --- |
| Autofac | 5.2.0 | 9.1.0 | 动态加载需要过滤可实例化类型 |
| Microsoft.CodeAnalysis.CSharp | 3.7.0 | 5.3.0 | 升级 Roslyn 编译和分析能力 |
| Newtonsoft.Json | 13.0.3 | 13.0.4 | JSON 依赖版本升级 |
| NLog | 4.7.4 | 6.1.3 | 日志框架升级 |

### 8.2 新增的 NuGet 包

| NuGet 包 | 当前版本 | 用途 |
| --- | ---: | --- |
| ReadLine | 2.0.1 | 控制台输入、行内编辑、历史记录和 Tab 交互 |
| System.CommandLine | 2.0.8 | 命令定义、解析、校验、帮助和补全 |

### 8.3 移除或被替代的 NuGet 包

以下旧版直接依赖已经被移除或由新实现替代：

- `CommandLineParser`
- `NancyHttpCommunicationModule`
- `Jtext103.CFET2.WebsocketEvent.WebsocketEventThing`
- `Microsoft.AspNet.Razor`
- `Microsoft.AspNet.WebApi.Client`
- `System.Diagnostics.PerformanceCounter`
- `System.ValueTuple`

移除 Nancy 和旧框架兼容依赖后，CFET2APP 的传递依赖数量明显减少，发布目录中的历史兼容 DLL 也相应减少。

## 9. 跨平台能力升级

旧版 `PcMonitorThing` 使用 Windows PerformanceCounter 获取可用内存：

```csharp
var ramCounter =
    new PerformanceCounter("Memory", "Available MBytes", true);
return ramCounter.NextValue();
```

当前版本改为使用 .NET GC 内存信息：

```csharp
var gcInfo = GC.GetGCMemoryInfo();
long availableBytes =
    gcInfo.TotalAvailableMemoryBytes - gcInfo.MemoryLoadBytes;

return availableBytes / (1024f * 1024f);
```

升级后的变化：

- 移除 `System.Diagnostics.PerformanceCounter` 依赖。
- 不再依赖 Windows 性能计数器。
- 可以在 Linux 和容器环境中获取内存状态。
- 获取结果可以反映容器内存限制。

## 10. 日志配置升级

当前版本新增 `appsettings.json`，其 `Logging:LogLevel` 用于配置 HTTP/WebSocket 服务的 ASP.NET Core 日志筛选。CFET 日志由 `NlogProvider.Config()` 代码配置：文件记录 Debug 及以上、控制台输出 Warn 及以上，当前不读取 `appsettings.json` 中的 NLog 节。

项目文件中配置了：

```xml
<None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</None>
```

因此，`appsettings.json` 会自动复制到构建和发布目录；其中的 Logging 设置影响 ASP.NET Core 日志。CFET 日志保留天数由 `GlobalConfig.json` 的 `LogRetentionDays` 控制。

## 11. 发布配置升级

发布脚本和 Visual Studio 发布配置已经从 `net6.0` 更新为 `net10.0`。

Windows x64 发布命令：

```shell
dotnet publish .\CFET2APP\CFET2APP.csproj \
    -f net10.0 \
    -r win-x64 \
    --configuration Release \
    --no-self-contained
```

Linux ARM64 发布命令：

```shell
dotnet publish \
    -f net10.0 \
    -r linux-arm64 \
    --configuration Release \
    --no-self-contained
```

当前发布方式仍为依赖框架发布，目标环境需要安装兼容的 .NET 运行时。

## 12. 项目内容调整

当前项目移除了旧版仓库中的 `Inventory` 目录，不再随 CFET2APP 仓库维护以下示例或配置库存：

- CFET2ConfigSave
- CFET2Epics
- CFET2Relay
- CFET2StateMachine

该调整减少了 CFET2APP 仓库中与主程序无直接关系的配置内容，使仓库职责更集中。

## 13. 当前构建状态与注意事项

当前解决方案已经可以在 `.NET 10` 环境下成功构建。

当前仍存在以下迁移注意事项：

1. 部分 EventHub 和 Hub 资源访问 API 已被标记为过时，后续需要逐步迁移到新版 Core API。
2. `FakeAI` 示例仍调用 `Thread.Abort()`；该 API 在现代 .NET 中不受支持，执行时可能抛出 `PlatformNotSupportedException`。
3. 动态 DLL 加载尚未实现程序集隔离、运行时卸载和真正的热更新。
4. `Expired` 中的旧版 README 保留历史框架和 Nancy 说明；当前使用方式以仓库根 README 为准。
5. 应避免将 `bin`、`obj`、日志和临时构建产物提交到版本库。

## 14. 升级总结

本次升级不只是将 CFET2APP 的目标框架切换到 `.NET 10`，同时完成了运行架构和用户交互方式的现代化：

- 使用 `.NET 10` 统一主程序运行环境。
- 使用新 HTTP 和 WebSocket 通信模块替换旧 Nancy 和 WebsocketEventThing。
- 使用 ReadLine 和 System.CommandLine 构建新的交互式控制台。
- 为命令、选项和资源路径提供智能 Tab 补全。
- 适配 Autofac 9，提升动态 DLL 加载稳定性。
- 移除旧框架兼容依赖，改善跨平台和容器运行能力。
- 更新日志、构建和发布配置。

当前版本已经完成主要升级路径，后续工作重点是迁移剩余过时 API，并进一步增强动态 DLL 的隔离、诊断和热更新能力。
