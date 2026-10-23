# 标准应用事件通信配置

`GlobalConfig.example.json` 是源码示例，不会自动覆盖运行目录中的 `GlobalConfig.json`。在应用工作目录准备配置；已有配置只补充所需字段。标准启动还使用 `CustomView/CustomViewConfig.json` 和 `thingConfig/`；空设备目录也应保留。CustomView 的最小配置是 `{"RegularMatches":{}}`。

```powershell
# 从应用工作目录执行；已有配置不覆盖。
if (-not (Test-Path -LiteralPath './GlobalConfig.json')) {
    Copy-Item -LiteralPath '<源码目录>/CFET2APP/Config/GlobalConfig.example.json' -Destination './GlobalConfig.json'
}
dotnet '<构建目录>/CFET2APP.dll'
```

所有配置在应用重启后生效。配置格式错误会中止启动；Broker 暂时不可用时应用继续提供 HTTP 服务并异步恢复 MQTT 连接。

| 使用方式 | EventPublishTransports | HttpEventSubscriptionTransport |
| --- | --- | --- |
| WS 默认发布及订阅 | `["WebSocket"]` | `"WebSocket"` |
| MQTT 发布及 HTTP 事件订阅 | `["Mqtt"]` | `"Mqtt"` |
| WS/MQTT 双发布，HTTP 订阅走 MQTT | `["WebSocket","Mqtt"]` | `"Mqtt"` |
| WS 发布，MQTT 仅订阅 | `["WebSocket"]` | `"Mqtt"` |

标准宿主始终注册 WS/MQTT 两个事件模块以保留显式协议订阅。即使使用默认 WS 配置，MQTT 也会连接 Broker，默认是 localhost:1883；没有本地 Broker 时可从诊断查看恢复状态。发布集合控制来源发布能力，不是连接启用开关。

MQTT 首版配置为 TCP，不包含 TLS 或凭据。MqttBrokerHost 不含协议或端口；端口单独使用整数 MqttBrokerPort。来源默认继承本次配置的应用 HTTP 基地址；需要公开地址与监听地址不同时，显式设置 MqttSourceHttpBaseUri，并确保订阅端使用相同来源 authority。来源不是 Broker 地址。

MqttClientId 为 null 或缺失时由模块生成；重连沿用。显式 ClientId 应由不同运行实例分别配置，避免 Broker 将相同 ClientId 的旧连接踢下线。

HTTP 资源初读仍使用 HTTP，HTTP 事件订阅按 HttpEventSubscriptionTransport 选择单一模块。显式 ws/mqtt 订阅独立保留；直接 mqtt 地址不提供资源 Get。双发布不跨协议去重。

运行后通过 HTTP GET 或 CLI 的已有资源读取方式查看：

- `/MQTTTCM/status`、`/MQTTTCM/printStatus`：模块、连接、队列及组件状态。
- `/MQTTTCM/subscriptions`、`/MQTTTCM/printSubscriptions`：当前业务订阅规则与需求发送诊断。
- `/MQTTTCM/observations`、`/MQTTTCM/printObservations`：本应用来源观察与租约状态。

状态查询不连接网络、不建立观察。规则 Ready 或 observe 的 Broker 接受只说明对应处理阶段，不证明来源在线或远端已建立观察。仅订阅模式下来源观察列表为空。
