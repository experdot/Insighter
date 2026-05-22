# Insighter

[English](./README.md)

Insighter 是一个本地优先的个人系统行为观测工作台。它运行在你的电脑上，采集一小组结构化系统事件，存储到 SQLite，通过本地 HTTP API 对外提供访问，并带有一套 Web 前端用于检索、配置和健康状态查看。

## 当前状态

当前仓库已经实现可运行的 v1：

- 基于 .NET 8 的本地宿主与 Minimal API
- SQLite 持久化
- 8 个 Windows 平台采集器
- React + TypeScript 前端
- `Dashboard`、`Logs`、`Collectors`、`Settings` 四个页面
- 中英文双语界面

当前平台实现以 Windows 为主，宿主项目目标框架为 `net8.0-windows`。

## 当前能力

Insighter 目前采集以下事件类型：

- `System.Startup`
- `System.Shutdown`
- `System.Sleep`
- `System.Resumed`
- `Session.Locked`
- `Session.Unlocked`
- `Window.Activated`
- `User.IdleStarted`
- `User.IdleEnded`
- `Clipboard.Changed`
- `Process.Started`
- `Process.Exited`
- `Power.SourceChanged`
- `Power.ChargingStateChanged`
- `Power.BatteryLevelChanged`
- `Network.Online`
- `Network.Offline`
- `Network.ActiveNetworkChanged`

同时支持：

- 采集器启用 / 禁用
- 采集器启动 / 停止
- 采集器配置更新
- Dashboard 聚合分析
- 日志过滤与分页
- 本地设置管理
- 面向隐私的默认脱敏策略

当前采集器包括：

- `SystemLifecycleCollector`：采集启动、关机、休眠、恢复信号。控制台模式下的关机事件仍然是尽力而为。
- `SessionStateCollector`：采集会话锁定与解锁状态变化。
- `ForegroundWindowCollector`：采集前台窗口与进程变化，支持标题截断和进程黑名单。
- `IdleCollector`：按可配置阈值采集空闲开始与结束事件，并可选择在锁屏时忽略。
- `ClipboardCollector`：采集剪贴板变化，支持采集模式、最大内容长度和仅文本限制。
- `ProcessLifecycleCollector`：采集进程启动与退出事件。优先使用 WMI 进程跟踪事件；如果 WMI 权限被拒绝，会自动回退到进程快照轮询，避免采集器故障退出。
- `PowerCollector`：采集电源来源、充电状态和电量阶梯变化。
- `NetworkConnectivityCollector`：采集在线 / 离线切换以及活动网络变化。

平台说明：

- `ProcessLifecycleCollector` 在 Windows 上标记为 `PartiallySupported`，因为轮询回退相比 WMI 事件精度更低。
- 轮询回退模式下，`parentProcessId` 和 `exitCode` 可能缺失，`executablePath` 也只是尽力获取。
- 本地 API 会通过 `GET /api/platform/capabilities` 以及采集器详情接口返回这些能力级别信息。

## v1 前端页面

- `Dashboard`：事件总览、时间线、事件分布、采集器健康状态
- `Logs`：结构化日志检索、筛选、分页、详情查看
- `Collectors`：运行状态、平台能力、启停控制、配置面板
- `Settings`：保留策略与隐私默认值、只读宿主信息

## 架构概览

```text
Collector -> EventSink -> Channel -> Validate -> Redact -> Persist -> API -> Web UI
```

项目结构：

```text
src/
  Insighter.Host/
  Insighter.Application/
  Insighter.Domain/
  Insighter.Contracts/
  Insighter.Infrastructure.Persistence/
  Insighter.Infrastructure.Platform.Windows/

tests/
  Insighter.Application.Tests/
  Insighter.IntegrationTests/

web/
  基于 Vite + React + TypeScript 的前端
```

核心边界：

- 采集器不直接写数据库
- API 通过应用服务访问数据，不在端点中直接拼 SQL
- 平台相关逻辑隔离在 Windows 基础设施层
- 日志模型采用固定头部字段 + 结构化 `Content` JSON

## 技术栈

后端：

- .NET 8
- ASP.NET Core Minimal API
- Generic Host
- SQLite
- Dapper 风格持久化层

前端：

- Vite
- React 19
- TypeScript
- Tailwind CSS v4
- Zustand
- i18next
- Recharts

## API 概览

主要本地接口：

- `GET /api/meta`
- `GET /api/collectors`
- `GET /api/collectors/{collectorId}`
- `POST /api/collectors/{collectorId}/start`
- `POST /api/collectors/{collectorId}/stop`
- `PUT /api/collectors/{collectorId}/config`
- `GET /api/logs`
- `GET /api/logs/{id}`
- `GET /api/dashboard/summary`
- `GET /api/dashboard/timeline`
- `GET /api/platform/capabilities`
- `GET /api/settings`
- `PUT /api/settings`

默认本地 API 地址：

```text
http://127.0.0.1:5078
```

## 快速开始

### 前置要求

- Windows
- .NET SDK 8+
- Node.js 20+
- `pnpm`

### 1. 还原并构建后端

```powershell
dotnet build Insighter.slnx
```

### 2. 安装前端依赖

```powershell
cd web
pnpm install
```

### 3. 以开发模式运行完整应用

终端 1：

```powershell
dotnet run --project src/Insighter.Host/Insighter.Host.csproj
```

终端 2：

```powershell
cd web
pnpm dev
```

访问地址：

- 前端开发服务：`http://127.0.0.1:5173`
- 本地 API / 宿主：`http://127.0.0.1:5078`

Vite 开发服务器会将 `/api` 请求代理到本地宿主。

## 构建前端并交给 Host 托管

前端构建产物会直接输出到：

```text
src/Insighter.Host/wwwroot
```

执行：

```powershell
cd web
pnpm build
```

然后启动宿主：

```powershell
dotnet run --project src/Insighter.Host/Insighter.Host.csproj
```

此时宿主会同时提供：

- `/` 下的 SPA 页面
- `/api/*` 下的本地接口

## 配置

主要运行时配置位于：

- [`src/Insighter.Host/appsettings.json`](./src/Insighter.Host/appsettings.json)

关键默认值：

- API 地址：`http://127.0.0.1:5078`
- 队列容量：`4096`
- 日志保留天数：`30`
- 查询最大分页：`500`
- 默认剪贴板模式：`Preview`
- 默认剪贴板最大长度：`200`
- 默认窗口标题最大长度：`200`

## 常用开发命令

前端：

```powershell
cd web
pnpm typecheck
pnpm lint
pnpm build
```

后端：

```powershell
dotnet build Insighter.slnx
dotnet test Insighter.slnx
```

## 隐私说明

Insighter 是本地优先产品，但它依然会处理敏感行为数据。v1 中已经具备：

- 剪贴板采集模式可配置
- 窗口标题支持截断
- 支持基于进程黑名单的窗口脱敏
- 在事件处理管道中先脱敏，再持久化

即便如此，在真实个人设备或工作设备上使用前，仍建议先审查相关设置。

## 后续方向

当前架构已经为以下方向预留空间：

- 更丰富的 Dashboard 聚合分析
- 通过受控 Function Call 边界实现 AI 日志分析
- macOS 与 Linux 平台适配层
- 更完整的观测与隐私控制能力

## 许可证

本项目采用 [MIT License](./LICENSE)。
