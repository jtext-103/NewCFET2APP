# 如何开发 CFET Thing

本文以仓库中的 [`ExampleThing`](ExampleThing/ExampleThing.cs) 为准，说明当前 CFET2 Core 中 Thing 的实现、挂载和资源访问方式。示例项目目标框架为 `netstandard2.0`，依赖 `CFET2Core`。

## 基本概念

Thing 是继承 `Jtext103.CFET2.Core.Thing` 的 .NET 类。挂载到 Hub 后，带有 CFET 特性的成员会被探测并暴露为资源（CR，资源路径）。当前实现中的资源类型只有：

- `Thing`：挂载后的对象节点。
- `Status`：只读资源，对应 `get`。
- `Config`：可读写资源，对应 `get` 和 `set`。
- `Method`：可执行资源，对应 `invoke`。

资源路径由 Thing 挂载路径和资源名组成。当前 `ExampleThing.getnum()` 声明了 `[Cfet2Status("getnuma")]`，将 Thing 挂载为 `/ExampleThing` 后，其资源路径是 `/ExampleThing/getnuma`。Thing 内部使用 `GetPathFor(nameof(getnum))` 可获得这个路径；`nameof` 使用 C# 成员名，`GetPathFor` 负责解析特性中的对外名称。

当前 `AccessAction` 只有 `get`、`set`、`invoke`，资源订阅不作为 HTTP action 暴露。事件订阅通过 Hub API 建立，事件消息由事件系统分发，远程场景由 WebSocket 通信模块传输。`[Cfet2Event]` 用于标识 Status/Config 资源支持原生事件：带该标记时由 Thing 主动发布事件，未带该标记时可由 Core 内部观测资源变化。

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

## 事件与资源订阅

### 如何选择事件发布方式

| 资源声明 | 谁发布变化事件 | Thing 需要做什么 |
| --- | --- | --- |
| Status/Config 未标记 `[Cfet2Event]` | 建立资源订阅后，Core 周期读取并在值变化时自动发布 `changed` | 提供可读取的受支持标量或容器，getter 必须无参数，无需调用 Publish |
| Status/Config 标记 `[Cfet2Event]` | Thing 在数据更新处主动发布 | 更新值后调用 `MyHub.PublishResource(GetPathFor(nameof(资源成员)), value)` |

`[Cfet2Event]` 只是原生事件能力标识。它不会自动监听属性赋值，也不会自动插入 Publish；标记后 Core 不再轮询该资源。如果希望写入 Config 或采集到新值时立即发布，应在 setter 或采集回调中显式发布。

### 方式一：Core 自动观测并发布事件

下面是完整的 Thing 示例。`Target` 没有 `[Cfet2Event]`，因此资源订阅建立后，Core 会自动检测其变化。

```csharp
using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Attributes;

public sealed class ObservedMeterThing : Thing
{
    [Cfet2Config(Name = "target")]
    public int Target { get; set; }

    [Cfet2Method]
    public void UpdateTarget(int value)
    {
        Target = value; // 无需手动 Publish，Core 在下一次观测到变化时发布。
    }
}
```

在宿主或已启动的 Thing 中建立订阅：

```csharp
var meter = new ObservedMeterThing();
MyHub.TryAddThing(meter, "/device", "meter");

// nameof 返回 "Target"，GetPathFor 解析 Name="target" 和实际挂载路径。
var subscription = MyHub.SubscribeResource(
    meter.GetPathFor(nameof(ObservedMeterThing.Target)),
    e => System.Console.WriteLine($"changed: {e.Sample.ObjectVal}"));

System.Console.WriteLine($"initial: {subscription.InitialValue.ObjectVal}");
meter.UpdateTarget(42);

// 保持 subscription 存活以继续接收事件；不再使用时执行：
// subscription.Dispose();
```

此处路径为 `/device/meter/target`。Core 先读取当前值作为 `InitialValue`，之后默认每 500 ms 观测一次，标量以相等性比较、受支持容器以独立快照内容比较判断变化。资源订阅、独立观察句柄和 AutoObserveList 常驻观察共享任务；最后一个观察需求释放后停止观测。仅声明资源不会启动观测，AutoObserveList 或独立观察句柄可在没有资源订阅时启动观测。

APP 可在启动目录的 `GlobalConfig.json` 中设置 `ResourceObservationIntervalMs`，正整数生效，缺失或非法时使用 500 ms。配置须在创建 Hub 前加载，修改后重启。轮询可能漏掉两次采样之间的短暂变化。数值标量支持 ResourceMinDelta 和 ResourceMinDeltaOverrides，默认阈值 0；浮点边界不加 epsilon，容器仍按内容比较。

自动观测支持数值、`bool`、`char`、`string`、枚举、`DateTime`、`DateTimeOffset`、`TimeSpan`、`Guid`，以及这些值类型的 `Nullable<T>`。`Status<T>`、`Config<T>`、`SampleBase<T>` 及其派生类按 `T` 判断。数组、精确 `List<T>` 和 `Dictionary<TKey, TValue>` 也支持自动观测，元素及字典值递归受支持，字典键限标量。自定义类/结构体、容器子类、接口和其他集合、object 或非泛型 ISample 声明仍不支持。无参数未标记资源在精确订阅时遇到不支持类型会抛 WrongResourceActionException；普通 Get/Set 不受影响。所有含参数 getter（含默认参数和 params）都不轮询，但精确 SubscribeResource 可按地址读取一次初值并接收主动事件。Method 不支持资源订阅；无参数未标记资源的精确资源订阅仅支持 changed。

### 方式二：在值更新时主动发布，使用 nameof 定位资源

下面同时演示 Status 采集更新和 Config setter 发布。两个资源都标记 `[Cfet2Event]`，不会再被 Core 周期观测。

```csharp
using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Attributes;

public sealed class NativeMeterThing : Thing
{
    private double temperature;
    private int target;

    [Cfet2Event]
    [Cfet2Status(Name = "temperature")]
    public double Temperature => temperature;

    [Cfet2Method]
    public void UpdateTemperature(double value)
    {
        if (temperature.Equals(value)) return;
        temperature = value;
        MyHub.PublishResource(GetPathFor(nameof(Temperature)), value);
    }

    [Cfet2Event]
    [Cfet2Config(Name = "target")]
    public int Target
    {
        get => target;
        set
        {
            if (target == value) return;
            target = value;
            MyHub.PublishResource(GetPathFor(nameof(Target)), value);
        }
    }
}
```

挂载为 `/device/meter` 后，`GetPathFor(nameof(Temperature))` 得到 `/device/meter/temperature`，`GetPathFor(nameof(Target))` 得到 `/device/meter/target`。先更新资源值，再发布，订阅回调通过 `EventArg.Sample` 读取推送值，默认事件类型为 `changed`。

`nameof(Temperature)` 本身只返回 C# 成员名 `Temperature`；应把它交给 `GetPathFor`，不要直接作为发布路径。这样修改成员名时可由编译器检查，同一 Thing 多实例挂载时也能使用各自路径。仅在 Thing 已挂载、Hub 已注入后执行发布；构造函数中不要发布，上例 setter 也不要用于构造阶段的初始化。业务若有多个并发更新来源，应自行保证更新与发布的顺序。

Get/Set 分开声明为方法时，同样使用实际成员名：

```csharp
private int setPoint;

[Cfet2Config(ConfigAction.Get, Name = "setPoint")]
public int ReadSetPoint() => setPoint;

[Cfet2Event]
[Cfet2Config(ConfigAction.Set, Name = "setPoint")]
public void WriteSetPoint(int value)
{
    if (setPoint == value) return;
    setPoint = value;
    MyHub.PublishResource(GetPathFor(nameof(WriteSetPoint)), value);
}
```

`GetPathFor(nameof(WriteSetPoint))` 会解析为当前 Thing 路径下的 `setPoint`，而不是 `WriteSetPoint`。分开的 Config 方法必须把 `[Cfet2Event]` 放在 Set 实现上；只标记 Get 实现会在资源探测时被拒绝。属性形式则直接标记可读写的 Config 属性。

上例自行跳过相同值，表示“变化时发布”；`PublishResource` 本身不会去重。对受支持标量或容器，可在 Thing 中调用 `PublishResourceIfChanged(nameof(资源成员), value)` 使用独立快照、变化比较和 minDelta；首次调用发布，返回 true 仅表示 Core 接受入队。带参数资源可通过 parameters 参数提供事件路径参数。直接 PublishResource 不使用阈值，也不更新该辅助函数基准。详见 [minDelta 使用示例](../newcfet2core/Code/CFET2Core/doc/主动变化发布/minDelta使用示例.md)。复杂对象也可走原生事件，但应发布稳定快照，不要在发布后继续原地修改同一实例。没有原生标记的资源若又手动发布，可能与自动观测产生重复通知，应按上表选择一种方式。

### 订阅、初始值与取消

资源订阅使用 `MyHub.SubscribeResource(...)`，返回 `ResourceSubscription`。调用方保存订阅对象，读取 `InitialValue`，不再使用时调用 `Dispose()`；也可保存其 `Token` 并调用 `MyHub.Unsubscribe(token)`。重复订阅前先释放旧订阅，避免遗留回调。

远程示例（在持有 `MyHub` 的宿主或 Thing 中执行）：

```csharp
var subscription = MyHub.SubscribeResource(
    "http://localhost:8002/ExampleThing/setNum",
    e => System.Console.WriteLine(e.Sample.ObjectVal));

System.Console.WriteLine(subscription.InitialValue.ObjectVal);
// 不再使用时：subscription.Dispose();
```

初始 Get 失败、返回 null 或 invalid sample 时会抛错，不建立后续订阅。远程资源需要同时注册资源通信模块和事件通信模块：HTTP 负责初始 Get，WebSocket 负责事件传输。远程调用返回表示初始 Get 和本地订阅注册完成，并不等待远端成功 ACK；之后仍可能收到拒绝 ACK，应查看 WebSocket 的 Pending/Active/Failed 状态、LastError 和日志。

初始 Get 与事件订阅不是一个原子操作，不保证覆盖两者之间的每次变化；断线期间的事件也不补发。应按业务需要决定是否重新读取当前值。

当前 `ExampleThing` 的实际演示为：

- `getnum()` 对外名为 `getnuma`，未标记原生事件，可通过资源订阅自动观测。
- `setNum`、`setNum2` 在 Set 方法中标记 `[Cfet2Event]`，写入后使用 `GetPathFor(nameof(setNum))` 等发布；当前示例每次写入都会发布，没有相同值过滤。
- `SubScribe()` 订阅远端 `setNum` 和 `setNum2`；`Start()` 中未自动调用它，需要显式触发。
- `publish()` 手动发布到 `getnuma`，与 `SubScribe()` 订阅的两个 Config 是不同资源，不能用它验证这两个 Config 的回调。

这些 Method 可分别通过 `PUT /ExampleThing/SubScribe`、`PUT /ExampleThing/unSubscribe`、`PUT /ExampleThing/publish` 调用。HTTP 只负责触发控制方法，事件不会在该请求中持续返回。

### 普通事件

非资源事件使用 `MyHub.Publish(path, eventType, sample)` 和 `MyHub.Subscribe(new EventFilter(path, eventType), handler)`，取消使用 `MyHub.Unsubscribe(token)`。普通事件订阅不会为本地资源启动自动观测；需要资源初始值和自动观测时使用 `SubscribeResource`。

`EventHub.Subscribe(...)`、`EventHub.Publish(...)` 已标记 `[Obsolete]`，新业务代码优先使用上述 Hub API。资源订阅不加入 `AccessAction`，也不是 HTTP get/set/invoke 的一种。

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
