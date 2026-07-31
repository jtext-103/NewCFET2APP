# 如何开发 CFET Thing

本文以仓库中的 [`ExampleThing`](ExampleThing/ExampleThing.cs) 为准，说明当前 CFET2 Core 中 Thing 的实现、挂载和资源访问方式。示例项目目标框架为 `netstandard2.0`，依赖 `CFET2Core`。

## 基本概念

Thing 是继承 `Jtext103.CFET2.Core.Thing` 的 .NET 类。挂载到 Hub 后，带有 CFET 特性的成员会被探测并暴露为资源（CR，资源路径）。当前实现中的资源类型只有：

- `Thing`：挂载后的对象节点。
- `Status`：只读资源，对应 `get`。
- `Config`：可读写资源，对应 `get` 和 `set`。
- `Method`：可执行资源，对应 `invoke`。

资源路径由 Thing 挂载路径和资源名组成。将名为 `ExampleThing` 的 Thing 挂载在 `/` 后，`getnum` Status 的路径为 `/ExampleThing/getnum`。

当前 `AccessAction` 只有 `get`、`set`、`invoke`。事件没有独立的 `subscribe` HTTP action 或 `[CFET2Event]` 特性；但 Thing 可以把订阅、取消订阅和发布封装为 `Cfet2Method`，从而通过 HTTP 的 `PUT` 调用作为第四类对外能力使用。事件消息仍由 EventHub（远程场景由 WebSocket 通信模块）分发。

## 创建 Thing

创建一个类并继承 `Thing`，引用以下命名空间：

```csharp
using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Attributes;
```

最小示例：

```csharp
public sealed class MeterThing : Thing
{
    private float target;

    [Cfet2Status]
    public float ReadValue()
    {
        return 12.5f;
    }

    [Cfet2Config]
    public float Target
    {
        get => target;
        set => target = value;
    }

    [Cfet2Method]
    public void Reset()
    {
        target = 0;
    }
}
```

未指定特性名称时，资源名使用成员名，因此上例会生成 `/MeterThing/ReadValue`、`/MeterThing/Target` 和 `/MeterThing/Reset`。可使用 `Name` 指定对外资源名，例如 `[Cfet2Status(Name = "value")]`。

## 生命周期与配置文件

`MyHub.TryAddThing(...)` 会依次完成 Hub 注入、资源探测和 `TryInit(initObject)` 调用；应用随后调用 `MyHub.StartThings()`，再执行每个 Thing 的 `Start()`。因此：

- 在 `TryInit` 中读取初始化参数、配置文件并完成不依赖其他已启动 Thing 的初始化。
- 在 `Start` 中执行依赖 Hub、通信模块或其他已挂载 Thing 的工作。
- 不要在构造函数中使用 `MyHub`，此时 Hub 尚未注入。

`ExampleThing` 在 `TryInit` 中使用 `getConfigFilePath(...)` 解析配置文件：

```csharp
public override void TryInit(object initObject)
{
    var configFile = getConfigFilePath((string)initObject);
    var config = new ExampleThingConfig(configFile);
}
```

代码挂载时可直接传入可访问的配置路径；动态挂载时，运行时会将 Thing 所在的 `thingConfig` 目录设为配置根目录，因此可传入同目录文件名。

## 暴露资源

### Status：只读状态

`[Cfet2Status]` 可修饰带 getter 的属性或方法。Status 只支持 `get`。

```csharp
[Cfet2Status(Name = "temperature")]
public double GetTemperature()
{
    return 23.5;
}
```

`ExampleThing.getnum()` 是当前的 Status 实现示例。

### Config：读写配置

`[Cfet2Config]` 可修饰属性或方法。一个 Config 必须同时具备 Get 与 Set 实现；挂载时缺少任一实现会失败。

对于简单配置，优先使用带公开 getter 和 setter 的属性：

```csharp
[Cfet2Config(Name = "setPoint")]
public float SetPoint { get; set; }
```

属性的默认 `ConfigAction` 为 `GetAndSet`。如需分开实现，使用相同资源名的两个方法：

```csharp
private float setPoint;

[Cfet2Config(ConfigAction.Get, Name = "setPoint")]
public float GetSetPoint() => setPoint;

[Cfet2Config(ConfigAction.Set, Name = "setPoint")]
public void SetSetPoint(float value) => setPoint = value;
```

`ConfigAction.Insert` 和 `ConfigAction.Delete` 虽在枚举中存在，但当前资源实现没有支持；不要用于新的 Thing。

`ExampleThing` 使用两个同名 `setNum` Config 方法分别实现 Get 和 Set。对于 Method 形式的 Set，最后一个参数是待写入的值，前面的参数被视为定位参数；新代码若不需要定位参数，建议只保留一个 `value` 参数。

### Method：可执行操作

`[Cfet2Method]` 只能修饰方法，资源只支持 `invoke`：

```csharp
[Cfet2Method(Name = "reset")]
public void Reset()
{
    // 执行设备操作
}
```

`ExampleThing.printA()` 与 `ExampleThing.publish()` 都是 Method 示例。

## 访问资源

Hub 的本地访问接口与 HTTP 映射如下：

| 资源类型 | Hub 调用 | HTTP 请求 |
| --- | --- | --- |
| Status | `MyHub.Get("/MeterThing/ReadValue")` | `GET /MeterThing/ReadValue` |
| Config 读取 | `MyHub.Get("/MeterThing/Target")` | `GET /MeterThing/Target` |
| Config 写入 | `MyHub.TrySetLocalResouceSample("/MeterThing/Target", 42f)` | `POST /MeterThing/Target` |
| Method | `MyHub.TryInvokeLocalResouceSample("/MeterThing/Reset")` | `PUT /MeterThing/Reset` |

HTTP 的有参数请求体使用 JSON 数组或对象。对象会按参数名匹配；例如对 `SetSetPoint(float value)` 写入数值：

```http
POST /MeterThing/setPoint
Content-Type: application/json

{"value": 42.0}
```

也可以使用显式输入包装：

```json
{
  "usingInputDict": true,
  "inputDict": { "value": 42.0 }
}
```

HTTP 模块会将 `GET` 映射为 `get`、`POST` 映射为 `set`、`PUT` 映射为 `invoke`。对不匹配的资源类型执行操作会得到错误结果。

## 事件：作为可远程控制的第四类能力

Thing 可以调用 `MyHub.EventHub.Publish(path, eventType, sample)` 发布事件。事件源使用路径和事件类型标识；`ExampleThing.publish()` 发布 `changed` 事件到 `/ExampleThing/getnum`：

```csharp
[Cfet2Method]
public void publish()
{
    MyHub.EventHub.Publish("/ExampleThing/getnum", "changed", 1);
}
```

订阅、取消订阅同样可以封装为 Method。`ExampleThing` 在启动时以及 `SubScribe()` 中订阅配置好的远程事件路径，并在 `unSubscribe()` 中释放返回的 `Token`：

```csharp
private Token subToken;
private readonly string remoteSubUri = "http://localhost:8002/ExampleThing/getnum";

[Cfet2Method]
public void SubScribe()
{
    subToken = MyHub.EventHub.Subscribe(
        new EventFilter(remoteSubUri, "changed"),
        eventHandler);
}

[Cfet2Method]
public void unSubscribe()
{
    MyHub.EventHub.Unsubscribe(subToken);
}
```

由于这三个成员是 Method，它们可通过 HTTP 资源调用：

| 目的 | HTTP 调用 |
| --- | --- |
| 发布事件 | `PUT /ExampleThing/publish` |
| 建立订阅 | `PUT /ExampleThing/SubScribe` |
| 取消订阅 | `PUT /ExampleThing/unSubscribe` |

HTTP 在这里用于访问 Thing 暴露的控制资源；订阅建立后的事件消息不在该 HTTP 请求中持续返回，而是由 `EventHub` 分发。本地订阅直接路由到回调；远程订阅需要已注册的 WebSocket 通信模块，由它建立远程订阅并回传事件。

`EventHub.Subscribe(EventFilter, Action<EventArg>)` 当前带有 `[Obsolete]` 标记，但 `ExampleThing` 和现有 WebSocket 通信模块仍使用并实现这一接口。调用方必须保存返回的 `Token`，并在不再需要时执行 `Unsubscribe(token)`；重复订阅前也应先确认旧 Token 是否已释放。

当前尚未实现的是独立的资源级 `Subscribe` action、`[CFET2Event]` 特性和自动内部观测。它们不影响以上通过 Method 控制 `EventHub` 发布、订阅和取消订阅的现有模式。

## 挂载 Thing

### 代码挂载

适合随宿主一起编译的 Thing。在 `Cfet2ProgramAddThingsPartial.cs` 的 `AddThings()` 中创建实例并挂载：

```csharp
var meter = new MeterThing();
MyHub.TryAddThing(meter, "/device", "meter", "./MeterConfig.json");
```

上述 Thing 的根路径为 `/device/meter`。代码挂载的项目或 NuGet 包必须先被 `CFET2APP` 引用。

### `thingConfig` 动态挂载

适合独立交付的 Thing。`DynamicThingsLoader` 会递归扫描运行目录下的 `thingConfig`，包含 `Config.json` 的目录会被视为一个 Thing：

```text
thingConfig/
  device/
    meter/
      Config.json
      MeterConfig.json
      dll/                  # 可选：此 Thing 的依赖 DLL
```

```json
{
  "Type": "YourNamespace.MeterThing",
  "InitObj": "MeterConfig.json"
}
```

此目录结构会将 `YourNamespace.MeterThing` 挂载到 `/device/meter`。`Type` 必须是完整命名空间和类名，`InitObj` 会传给 `TryInit(...)`。

动态加载的 DLL 可以放在运行目录的 `thingDll` 第一层，或放在各 Thing 配置目录的 `dll` 子目录。类型必须是非抽象类，拥有可供 Autofac 创建的公共构造函数，并且其依赖必须可解析。

## 开发检查

- 为每个公开资源选择正确特性：只读状态用 `Cfet2Status`，读写配置用 `Cfet2Config`，命令和事件控制入口用 `Cfet2Method`。
- 确认同一 Thing 中资源名唯一；Config 的 Get 和 Set 两个实现必须使用相同名称。
- 用 `TryInit` 读取配置，用 `Start` 启动依赖 Hub 的逻辑。
- 在实际运行目录准备 `GlobalConfig.json`、`thingConfig` 和需要的 DLL。
- 启动后使用 `list --thing`、`list --all --detail` 或 `get`、`set`、`invoke` 命令核对已挂载资源。
