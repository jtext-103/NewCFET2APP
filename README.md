# CFET2 App

CFET2 的宿主应用与交互式命令行。它初始化 `CFET2Host`、挂载 Thing 和通信模块，并提供资源浏览与调用 CLI。

## 功能

- 基于 .NET 10 的控制台宿主程序。
- 提供 `get`、`set`、`invoke`、`list`、`goto`、`exit` 等命令及交互式补全。
- 使用 Autofac 装配应用；NLog 负责 CFET 日志，`appsettings.json` 的 `Logging` 节配置 ASP.NET Core 服务日志。
- 包含 `ExampleThing`，可作为 Thing 开发和动态加载的参考。
- 支持加载 HTTP 与 WebSocket 通信模块。

## 快速开始

需要 Visual Studio 2026 及 .NET 10 开发环境。

### 开发启动

1. 使用 Visual Studio 2026 打开 `CFET2APP.sln`。
2. 将 `CFET2APP` 设为启动项目。
3. 确认启动工作目录中已准备 `GlobalConfig.json`、`thingConfig` 和按需提供的 `thingDll`。
4. 使用 F5 启动并调试。

### 无交互启动

在包含运行配置的目录执行 `CFET2APP.exe --no-cli`，即可启动通信模块和 Thing 并持续运行，不进入交互式 CLI，适合容器和后台宿主。该分支持续等待，退出由外部进程管理负责。

### 运行已编译程序

从包含运行配置的输出目录直接启动 `CFET2APP.exe`；例如：

```powershell
cd .\CFET2APP\bin\Debug\net10.0
.\CFET2APP.exe
```

当前程序按进程工作目录读取 `GlobalConfig.json`、`thingConfig` 和 `thingDll`。发布或复制程序时，必须将这些运行配置一并放入启动目录；不能在缺少这些文件的仓库根目录直接启动。


启动后输入 `help` 或具体命令的 `--help` 查看帮助；交互提示符为 `Cfet2>`。

## 配置与扩展

### 运行配置

程序在启动时从**进程工作目录**读取 `GlobalConfig.json`；该文件为必需配置。`IP`、`Port` 和 `Accept` 均为必填项，`LogRetentionDays` 可选。

```json
{
  "IP": "http://localhost",
  "Port": "8002",
  "Accept": "Json",
  "LogRetentionDays": 30
}
```

- `IP` 和 `Port` 共同构成 HTTP 服务地址。
- `Accept` 会传给 HTTP 通信模块，决定其接受的数据格式。
- WebSocket 通信模块使用 `IP` 与 `Port + 1` 建立服务地址。
- `LogRetentionDays` 控制按日期生成的日志文件保留天数；未提供、为空或非法时默认保留 7 天，设为 `0` 时不启动日志清理任务。
- `ResourceObservationIntervalMs` 设置未标记 `[Cfet2Event]` 的标量资源自动观测周期（毫秒）。正整数生效；未提供、为空、非整数或不大于 0 时使用默认 500 ms。启动时先加载配置，再创建 Hub；修改配置后需重启，不支持自动热更新。

### WebSocket 重订阅与并发配置

`GlobalConfig.json` 可设置 `WebSocketResubscribeIntervalMs`，控制重连恢复时相邻订阅发送的间隔。APP 默认 10 ms；0 表示不额外等待；缺失、空、负数或非整数回退到 10 ms。APP 会将该值传给模块的 `ResubscribeInterval`；直接使用模块而不覆盖该选项时默认是 50 ms。

启动前还可设置以下环境变量，只有正整数生效，其余值保留模块默认值：

| 环境变量 | 对应选项 | 模块默认值 |
| --- | --- | --- |
| `CFET2_WEBSOCKET_MAX_CONCURRENT_FAN_OUT_EVENTS` | `MaxConcurrentFanOutEvents` | `Math.Max(1, Environment.ProcessorCount)` |
| `CFET2_WEBSOCKET_MAX_FAN_OUT_CONCURRENCY` | `MaxFanOutConcurrency` | `Math.Max(1, Environment.ProcessorCount * 2)` |

APP 默认挂载 `/WebSocketCM` 诊断 Thing，例如 `get /WebSocketCM/status`、`get /WebSocketCM/remoteClients`。总览包含 Pending/Active/Failed 订阅计数；模块的 `Started` 状态仅表示生命周期已启动，不代表所有远程订阅已成功。

### 日志

运行目录中的 `appsettings.json` 会由 HTTP 和 WebSocket 服务创建的 ASP.NET Core `WebApplication` 自动加载。其中 `Logging:LogLevel` 控制 Microsoft / ASP.NET Core 日志筛选，级别必须使用完整枚举名称，例如 `Information`、`Warning`、`Error`；

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

CFET 自身日志则在启动时由 `NlogProvider.Config()` 以代码配置：文件日志写入启动目录的 `Log_yyyy-MM-dd.txt`，记录 `Debug` 及以上级别；控制台输出 `Warn` 及以上级别。`appsettings.json` 中的 `NLog` 节当前没有被该 Provider 读取，修改它不会改变 CFET 文件日志规则。日志清理由 `GlobalConfig.json` 中的 `LogRetentionDays` 控制。

### 挂载 Thing

应用在 `Cfet2ProgramAddThingsPartial.cs` 的 `AddThings()` 中挂载 Thing。`TryAddThing(...)` 会注入 Hub 并调用 `Thing.TryInit(...)`，随后启动流程统一调用 `Thing.Start()`。

#### 代码挂载

适合随应用编译、引用关系固定的 Thing。先让 `CFET2APP` 引用 Thing 项目或 NuGet 包，再在 `AddThings()` 中创建并挂载实例：

```csharp
using YourNamespace;

var deviceThing = new DeviceThing();
MyHub.TryAddThing(deviceThing, "/", "DeviceThing", "./DeviceConfig.json");
```

Thing 的最终资源路径为 `mountPath + name`；上例为 `/DeviceThing`，而 `mountPath` 为 `/device`、`name` 为 `meter` 时为 `/device/meter`。第四个参数会传给 `TryInit(...)`；需要配置文件的 Thing 可自行读取该路径。仓库中的 `CustomViewThing` 是当前启用的代码挂载示例。

#### `thingConfig` 动态挂载

适合在不修改宿主代码的情况下挂载独立编译的 Thing。`DynamicThingsLoader` 会递归扫描运行目录下的 `./thingConfig`，每个包含 `Config.json` 的目录都会被识别为一个 Thing。目录相对于 `thingConfig` 的位置决定挂载路径：

```text
thingConfig/
  device/
    meter/
      Config.json
      MeterConfig.json
      dll/                  # 可选：该 Thing 的依赖 DLL
```

```json
{
  "Type": "YourNamespace.MeterThing",
  "InitObj": "MeterConfig.json"
}
```

上述配置会把 `YourNamespace.MeterThing` 挂载到 `/device/meter`。`Type` 必须是完整命名空间和类名，`InitObj` 会传给 `TryInit(...)`。当前实现会将该 Thing 的配置目录作为配置文件根目录，因此 Thing 可通过 `getConfigFilePath("MeterConfig.json")` 读取同目录配置。

动态加载的 Thing DLL 必须能被运行程序加载：可放入运行目录下的 `./thingDll`（当前仅扫描第一层），或放入对应 Thing 配置目录的 `dll` 子目录。Thing 类型必须是可由 Autofac 创建的非抽象类，其依赖也必须可解析。当前动态加载示例位于 `CFET2APP/bin/Debug/net10.0/thingConfig/ExampleThing`，挂载路径为 `/ExampleThing`。

- [如何做一个 CFET Thing](HowToMakeCfetThings.md) 提供 Thing 开发参考。
## 文档

- [提交与依赖交付说明](Doc/提交与依赖交付说明.md)

- [新命令行交互功能](<Doc/newcfet2app 新命令行交互功能.md>)
- [升级内容](<Doc/newcet2app 升级内容.md>)
- [历史 README](<Expired/README.NET6编译说明.md>)
