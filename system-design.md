# Insighter 系统设计文档

## 1. 文档目标

本文档定义 `Insighter` 的第一阶段系统设计，作为后续 AI 与人工开发共同遵循的实现基线。

本阶段目标：

- 优先开发后端
- 建立可扩展的本地日志采集平台
- 确保采集器架构支持后续跨平台扩展到 Windows、macOS、Linux
- 为后续前端 Dashboard、日志检索、AI Chat 提供稳定 API
- 为后续 AI Function Call 查询日志打好边界

本阶段不实现前端页面，但需要为前端保留清晰接口与契约。

---

## 2. 产品定位

`Insighter` 是一个运行在用户电脑本地的日志记录与 AI 分析工具。

系统由三部分组成：

- 本地事件采集与日志存储
- 本地 HTTP API
- 纯前端界面

核心能力：

- 持续监控本机指定系统事件
- 将事件保存为结构化日志
- 支持日志查询、过滤、聚合分析
- 支持 AI Chat 通过受控 Function Call 查询日志并回答用户问题

---

## 3. 当前阶段范围

第一版后端支持以下采集事件：

- 开机
- 关机
- 锁屏
- 解锁
- 窗口切换
- 系统空闲开始
- 系统空闲结束
- 剪贴板变更

第一版必须支持：

- 采集器启用/禁用
- 采集器参数配置
- 采集器状态查询
- 日志查询与过滤
- 为后续 AI 查询预留日志查询服务层

第一版明确不做：

- 前端页面实现
- 复杂插件热加载（但需考虑该设计）
- 键盘原始输入记录
- 全量录屏/截图

---

## 4. 技术栈

### 4.1 后端

- `.NET 8`
- `ASP.NET Core 8`
- `Generic Host`
- `Minimal API`
- `BackgroundService`
- `SQLite`

建议配套：

- `Dapper` 作为首选数据访问方案
- `System.Threading.Channels` 作为事件管道
- `Microsoft.Extensions.*` 体系完成依赖注入、配置、日志、宿主生命周期

### 4.2 前端

前端后续采用以下技术栈，但当前阶段不开发：

- `Vite`
- `React`
- `TypeScript`
- `Tailwind CSS`
- `shadcn/ui`
- `Zustand`

---

## 5. 设计原则

后续所有代码与 AI 生成内容必须遵守以下原则：

1. 采用模块化单体，不提前拆分分布式架构。
2. 采集器不得直接访问数据库。
3. 采集器不得直接暴露 HTTP API。
4. 平台相关逻辑必须隔离在平台适配层。
5. 日志主表采用固定头部字段加 `Content JSON` 的模型。
6. 常用过滤条件必须落在顶层字段，不依赖 JSON 全表扫描。
7. 所有 AI 查询必须经过受控查询服务，不允许模型直接访问仓储层。
8. 隐私相关内容必须支持脱敏与模式控制，尤其是剪贴板与窗口标题。
9. 所有采集器必须支持启停、状态查询、配置更新。
10. 后续 macOS/Linux 支持通过新增适配器完成，不重写上层业务模型。

---

## 6. 总体架构

系统采用本地单进程宿主，内部划分为多个逻辑层。

### 6.1 架构分层

- `Host`
- `Application`
- `Domain`
- `Infrastructure.Persistence`
- `Infrastructure.Platform.Windows`
- `Infrastructure.Platform.MacOS`
- `Infrastructure.Platform.Linux`
- `Contracts`

### 6.2 依赖方向

必须遵循以下依赖方向：

- `Host` -> `Application`
- `Application` -> `Domain`
- `Infrastructure.*` -> `Application` + `Domain`
- `Contracts` 只承载跨层公共 DTO/协议，不反向依赖具体实现

禁止：

- `Domain` 依赖 `Infrastructure`
- `Collector` 直接依赖数据库实现
- API 层直接拼 SQL

### 6.3 运行模型

单个本地进程同时承担：

- 采集器宿主
- 日志处理管道
- SQLite 持久化
- 本地 HTTP API
- 后续 AI 接口承载

---

## 7. 推荐项目结构

建议采用如下解决方案结构：

```text
Insighter.sln

src/
  Insighter.Host/
  Insighter.Application/
  Insighter.Domain/
  Insighter.Contracts/
  Insighter.Infrastructure.Persistence/
  Insighter.Infrastructure.Platform.Windows/
  Insighter.Infrastructure.Platform.MacOS/
  Insighter.Infrastructure.Platform.Linux/

docs/
  system-design.md
```

各项目职责：

- `Insighter.Host`
    - 程序入口
    - DI 注册
    - Minimal API
    - Hosted Services

- `Insighter.Application`
    - 用例服务
    - 采集器管理器
    - 查询服务
    - 事件管道编排
    - 配置管理
    - 隐私脱敏策略

- `Insighter.Domain`
    - 核心模型
    - 枚举
    - 值对象
    - 采集器与事件抽象接口

- `Insighter.Contracts`
    - API 请求/响应 DTO
    - 查询对象
    - 传输层契约

- `Insighter.Infrastructure.Persistence`
    - SQLite 实现
    - 仓储实现
    - 初始化与迁移

- `Insighter.Infrastructure.Platform.*`
    - 各操作系统下的底层监控实现
    - 平台能力探测

---

## 8. 核心领域模型

### 8.1 日志事件模型

日志主表模型固定为：

- `Id`
- `OccurredAtUtc`
- `EventType`
- `CollectorId`
- `Severity`
- `UserSessionId`
- `MachineId`
- `Content`

说明：

- 前 7 个字段为固定头部字段
- `Content` 为合法 JSON 字符串
- 采集器自行定义 `Content` 内部内容
- `Content` 仍然必须遵守统一外壳结构

### 8.2 Content JSON 规范

所有事件的 `Content` 必须遵守如下顶层结构：

```json
{
    "schemaVersion": 1,
    "platform": "windows",
    "data": {}
}
```

约束：

- `schemaVersion` 必填，用于未来兼容
- `platform` 必填，记录事件来源平台
- `data` 必填，承载采集器具体事件内容

禁止直接把任意文本塞入 `Content` 而不定义结构。

### 8.3 事件类型命名

事件类型必须平台无关，推荐命名：

- `System.Startup`
- `System.Shutdown`
- `Session.Locked`
- `Session.Unlocked`
- `Window.Activated`
- `User.IdleStarted`
- `User.IdleEnded`
- `Clipboard.Changed`

---

## 9. 采集器架构

### 9.1 采集器抽象

每个采集器实现统一接口：

- `ICollector`

建议最小职责：

- `CollectorId`
- `DisplayName`
- `StartAsync`
- `StopAsync`
- `UpdateConfigAsync`
- `GetStatusAsync`

采集器本身只负责：

- 订阅平台事件或轮询平台状态
- 组装统一事件模型
- 写入 `IEventSink`

采集器不得直接：

- 写数据库
- 提供 HTTP 接口
- 读取其他采集器内部状态

### 9.2 采集器管理器

采集器统一由 `ICollectorManager` 管理，职责包括：

- 启动全部启用的采集器
- 停止指定采集器
- 更新采集器配置
- 汇总采集器状态
- 在故障后执行退避重试
- 根据平台能力决定可运行性

### 9.3 采集器状态机

每个采集器必须具有以下状态：

- `Disabled`
- `Starting`
- `Running`
- `Stopping`
- `Faulted`
- `Unsupported`

说明：

- `Unsupported` 用于当前平台不支持该能力
- `Faulted` 不得导致整个宿主崩溃

### 9.4 第一版采集器列表

- `SystemLifecycleCollector`
- `SessionStateCollector`
- `ForegroundWindowCollector`
- `IdleCollector`
- `ClipboardCollector`

---

## 10. 平台适配层

### 10.1 设计目的

为支持 Windows、macOS、Linux，需要将底层操作系统调用隔离在平台适配层。

上层采集器使用平台无关接口，不直接调用 Win32、macOS API 或 Linux 桌面环境能力。

### 10.2 推荐平台接口

- `ISystemLifecycleMonitor`
- `ISessionStateMonitor`
- `IForegroundWindowMonitor`
- `IIdleMonitor`
- `IClipboardMonitor`
- `IPlatformCapabilityProvider`

### 10.3 跨平台支持策略

跨平台目标不是保证完全一致的功能，而是保证：

- 接口一致
- 事件模型一致
- 平台能力可探测

能力状态至少包括：

- `Supported`
- `PartiallySupported`
- `Unsupported`

说明：

- Linux 尤其在 `Wayland` 环境下，窗口切换和剪贴板能力可能受限
- 前端与 API 需要能够展示当前平台能力结果

---

## 11. 事件处理管道

### 11.1 设计目标

日志写入过程采用统一事件管道，避免采集器直接落库。

推荐管道：

`Collector -> EventSink -> Channel -> Enrich -> Validate -> Redact -> Persist`

### 11.2 各阶段职责

- `EventSink`
    - 采集器统一入口
    - 接收事件写入内存队列

- `Channel`
    - 缓冲事件
    - 削峰
    - 解耦采集与持久化

- `Enrich`
    - 填充机器信息
    - 填充会话信息
    - 填充平台信息

- `Validate`
    - 校验基础字段完整性
    - 校验 `Content` JSON 结构

- `Redact`
    - 对敏感信息进行脱敏或裁剪

- `Persist`
    - 批量写入存储

### 11.3 管道实现建议

- 使用 `System.Threading.Channels`
- 支持批量写入
- 支持背压与容量限制
- 支持宿主关闭前的优雅清空

---

## 12. 持久化设计

### 12.1 数据库选型

第一版数据库采用 `SQLite`。

原因：

- 嵌入式部署简单
- 无需单独数据库服务
- 跨平台
- 适合本地单机工具
- 支持 JSON 函数

架构上保留仓储抽象，未来可以新增 `PostgreSQL` 实现。

### 12.2 仓储抽象

当前至少保留以下仓储接口：

- `ILogEventRepository`
- `ICollectorConfigRepository`

推荐同时预留：

- `ICollectorStateRepository`
- `IAppSettingsRepository`

### 12.3 建议数据表

第一版建议至少包含以下表：

#### LogEvents

- `Id`
- `OccurredAtUtc`
- `EventType`
- `CollectorId`
- `Severity`
- `UserSessionId`
- `MachineId`
- `Content`

#### CollectorConfigs

- `CollectorId`
- `Enabled`
- `Version`
- `UpdatedAtUtc`
- `SettingsJson`

#### CollectorStates

- `CollectorId`
- `Status`
- `LastHeartbeatUtc`
- `LastError`
- `UpdatedAtUtc`

#### AppSettings

- `Key`
- `Value`
- `UpdatedAtUtc`

### 12.4 SQLite 表设计建议

- `OccurredAtUtc` 使用整数时间戳或 ISO 8601 UTC 字符串，需统一全局标准
- `Content` 使用 `TEXT` 存储，并加 `json_valid` 约束
- 对常用头部字段建立索引

建议首批索引：

- `(OccurredAtUtc DESC)`
- `(EventType, OccurredAtUtc DESC)`
- `(CollectorId, OccurredAtUtc DESC)`
- `(UserSessionId, OccurredAtUtc DESC)`
- `(MachineId, OccurredAtUtc DESC)`

如后续窗口与剪贴板查询频繁，可追加 JSON 路径索引。

### 12.5 迁移策略

第一版建议：

- 应用启动时自动初始化数据库
- 保持幂等
- 将 SQL 脚本版本化管理

禁止将数据库建表逻辑散落在业务代码中。

---

## 13. 配置体系

### 13.1 配置分层

配置分为三类：

- 全局应用配置
- 采集器配置
- 敏感配置

### 13.2 全局应用配置

示例：

- 数据库存储路径
- 日志保留天数
- 队列容量
- 默认脱敏策略
- API 监听地址

### 13.3 采集器配置

每个采集器配置采用统一外壳：

- `CollectorId`
- `Enabled`
- `Version`
- `SettingsJson`

示例：

- `IdleCollector`
    - 空闲阈值秒数
    - 是否忽略锁屏期间空闲

- `ClipboardCollector`
    - 采集模式
    - 最大内容长度
    - 是否仅记录文本

### 13.4 敏感配置

后续 AI 功能需要保存：

- LLM Provider
- Base URL
- API Key
- Model

敏感配置不得以明文普通配置方式处理，应通过 `ISecretStore` 管理。

---

## 14. 隐私与安全设计

### 14.1 基本原则

本产品涉及用户本机行为数据，必须将隐私控制视为核心设计要求。

### 14.2 剪贴板策略

剪贴板采集模式必须支持：

- `Off`
- `MetadataOnly`
- `Preview`
- `FullText`

默认建议：

- 第一版默认值使用 `MetadataOnly`

### 14.3 窗口标题策略

窗口标题可能包含文档名、URL、私人信息，必须支持：

- 标题截断
- 应用黑名单
- 按规则脱敏

### 14.4 AI 查询安全边界

后续 AI 只能调用受控 Function Call，不得直接访问数据库。

必须限制：

- 可查询时间范围
- 返回最大条数
- 可见字段
- 敏感数据脱敏结果

---

## 15. 查询与聚合服务

### 15.1 查询层职责

日志查询不得由 API 控制器直接拼接数据库查询，必须通过服务层。

建议保留：

- `ILogQueryService`
- `ILogAggregationService`

### 15.2 查询能力

第一版查询应支持：

- 时间范围过滤
- 事件类型过滤
- 采集器过滤
- 严重级别过滤
- 会话过滤
- 机器过滤
- 分页
- 按时间倒序排序

如需要，可增加受控 JSON 条件过滤，但禁止开放任意 JSONPath 执行。

### 15.3 聚合能力

为后续 Dashboard 预留：

- 指定时间范围内事件总数
- 窗口切换次数
- 空闲时长统计
- 活跃时间段统计
- 剪贴板事件数量

---

## 16. API 设计原则

当前阶段优先后端，API 需要为未来前端与 AI 共用。

### 16.1 API 风格

- 使用 `ASP.NET Core Minimal API`
- 本地 HTTP API
- 统一错误模型
- 统一分页模型

### 16.2 第一版建议接口

#### 采集器管理

- `GET /api/collectors`
- `GET /api/collectors/{collectorId}`
- `POST /api/collectors/{collectorId}/start`
- `POST /api/collectors/{collectorId}/stop`
- `PUT /api/collectors/{collectorId}/config`

#### 日志查询

- `GET /api/logs`
- `GET /api/logs/{id}`

#### 聚合查询

- `GET /api/dashboard/summary`
- `GET /api/dashboard/timeline`

#### 平台能力

- `GET /api/platform/capabilities`

#### 应用配置

- `GET /api/settings`
- `PUT /api/settings`

### 16.3 API 约束

- 所有时间使用 UTC
- 所有接口 DTO 独立定义，不直接暴露数据库模型
- 所有列表查询必须分页

---

## 17. 后续 AI 集成设计

当前阶段不实现完整 AI 功能，但架构必须预留其边界。

### 17.1 AI 架构原则

- 前端负责配置界面，不直接调用 LLM
- 后端保存模型配置并负责实际调用
- AI 通过 Function Call 查询日志服务

### 17.2 AI 允许调用的服务边界

建议后续仅暴露受控工具：

- `search_logs`
- `get_timeline`
- `get_app_usage`
- `get_log_context`

禁止直接暴露：

- 任意 SQL
- 任意 JSON 查询
- 任意文件访问

---

## 18. 前端约束

虽然前端暂不开发，但本设计文档明确前端基础约束。

### 18.1 技术栈

- `Vite`
- `React`
- `TypeScript`
- `Tailwind CSS`
- `shadcn/ui`
- `Zustand`

### 18.2 前端职责

后续前端负责：

- Dashboard
- 日志检索与过滤
- 采集器配置
- LLM 配置
- AI Chat

### 18.3 前后端边界

前端不得：

- 直接访问数据库
- 直接访问采集器内部对象
- 直接持有明文敏感密钥进行推理调用

---

## 19. 非功能性要求

### 19.1 可扩展性

- 新增采集器时，不修改现有采集器核心逻辑
- 新增平台时，主要新增平台适配器实现
- 新增数据库时，主要新增仓储实现

### 19.2 稳定性

- 单个采集器故障不得导致整个进程退出
- 写库失败需要记录错误并允许后续恢复
- 应支持优雅关闭

### 19.3 性能

- 高频事件写入需支持批量落库
- 避免每条事件单独打开数据库连接
- 对窗口切换与剪贴板变化考虑节流与去重

### 19.4 可测试性

必须通过接口隔离以下能力，方便单元测试：

- 时间
- 平台事件源
- 机器信息
- 用户会话信息
- 数据访问

---

## 20. 第一阶段实施顺序

推荐按以下顺序开发：

1. 建立解决方案与项目分层
2. 定义 `Domain` 核心模型与接口
3. 实现 `SQLite` 持久化与数据库初始化
4. 实现事件管道与 `IEventSink`
5. 实现 `ICollectorManager`
6. 优先实现 Windows 平台适配器
7. 实现第一版 5 个采集器
8. 实现日志查询与采集器配置 API
9. 补充平台能力接口
10. 为后续 AI 与前端保留 DTO 与查询边界

---

## 21. AI 编码必须遵守的硬性规则

后续任何 AI 生成代码必须遵守：

1. 目标框架必须是 `.NET 8`。
2. API 层必须使用 `ASP.NET Core`。
3. 采集器必须通过接口注册到统一管理器。
4. 采集器不得直接写库。
5. 所有日志必须写入统一事件模型。
6. `Content` 必须为合法 JSON，并带 `schemaVersion`、`platform`、`data`。
7. 数据访问必须通过仓储接口，不允许业务层直接操作 SQLite 细节。
8. 平台相关代码必须放在对应 `Infrastructure.Platform.*` 项目。
9. 所有公共 API 输入输出使用独立 DTO。
10. 所有敏感数据功能必须预留脱敏控制点。

---

## 22. 当前结论

`Insighter` 第一阶段应实现为一个后端优先的本地日志采集平台，采用模块化单体架构，以 `SQLite + 固定头部字段 + Content JSON` 为核心持久化模型，以统一采集器抽象、平台适配层、事件处理管道、查询服务层和隐私控制为架构骨架。

后续无论是开发前端页面，还是引入 AI Chat 与 Function Call，都必须建立在本设计文档之上，不应绕过既定边界直接耦合底层实现。
