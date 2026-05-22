# Insighter

[中文说明](./README.zh-CN.md)

Insighter is a local-first observability desk for personal system activity. It runs on your machine, collects a small set of structured system events, stores them in SQLite, exposes a local HTTP API, and ships with a web dashboard for search, configuration, and health monitoring.

## Status

This repository already includes a working v1:

- .NET 8 local host with Minimal API
- SQLite persistence
- Eight Windows platform collectors
- React + TypeScript frontend
- Dashboard, Logs, Collectors, and Settings pages
- English and Simplified Chinese UI

Current platform implementation is Windows-first. The host project targets `net8.0-windows`.

## What It Does

Insighter currently collects these event types:

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

It also supports:

- Collector enable / disable
- Collector start / stop
- Collector config updates
- Dashboard aggregation
- Log filtering and pagination
- Local settings management
- Privacy-oriented redaction defaults

Current collectors:

- `SystemLifecycleCollector`: startup, shutdown, sleep, and resume signals. Shutdown remains best-effort in console mode.
- `SessionStateCollector`: session lock and unlock state changes.
- `ForegroundWindowCollector`: active window and process changes with title truncation and process blacklist support.
- `IdleCollector`: idle start and end detection with configurable threshold and optional ignore-while-locked behavior.
- `ClipboardCollector`: clipboard changes with configurable capture mode, max content length, and text-only guardrail.
- `ProcessLifecycleCollector`: process start and exit events. It prefers WMI process trace events and automatically falls back to snapshot polling if WMI access is denied, so the collector stays running instead of faulting.
- `PowerCollector`: power source, charging state, and battery level step changes.
- `NetworkConnectivityCollector`: online and offline transitions plus active network changes.

Platform notes:

- `ProcessLifecycleCollector` is reported as `PartiallySupported` on Windows because polling fallback has lower fidelity than WMI trace events.
- In polling fallback, `parentProcessId` and `exitCode` can be unavailable, and `executablePath` is best-effort.
- The local API exposes these support levels through `GET /api/platform/capabilities` and the collector detail payloads.

## Screens in v1

- `Dashboard`: event volume, timeline, distribution, collector health
- `Logs`: structured log search, filters, pagination, detail dialog
- `Collectors`: runtime status, platform support, start / stop, config sheet
- `Settings`: retention and privacy defaults, read-only host info

## Architecture

```text
Collector -> EventSink -> Channel -> Validate -> Redact -> Persist -> API -> Web UI
```

Project layout:

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
  Vite + React + TypeScript frontend
```

Key boundaries:

- Collectors do not write to the database directly
- API uses application services instead of raw SQL in endpoints
- Platform-specific logic is isolated in the Windows infrastructure layer
- Logs use fixed header fields plus structured `Content` JSON

## Stack

Backend:

- .NET 8
- ASP.NET Core Minimal API
- Generic Host
- SQLite
- Dapper-style persistence layer

Frontend:

- Vite
- React 19
- TypeScript
- Tailwind CSS v4
- Zustand
- i18next
- Recharts

## API Overview

Main local endpoints:

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

Default local API URL:

```text
http://127.0.0.1:5078
```

## Quick Start

### Prerequisites

- Windows
- .NET SDK 8+
- Node.js 20+
- `pnpm`

### 1. Restore and build backend

```powershell
dotnet build Insighter.slnx
```

### 2. Install frontend dependencies

```powershell
cd web
pnpm install
```

### 3. Run the full app in development

Terminal 1:

```powershell
dotnet run --project src/Insighter.Host/Insighter.Host.csproj
```

Terminal 2:

```powershell
cd web
pnpm dev
```

Open:

- Frontend dev server: `http://127.0.0.1:5173`
- Local API / host: `http://127.0.0.1:5078`

The Vite dev server proxies `/api` requests to the local host.

## Build Frontend for Host Serving

The frontend build output is emitted directly into:

```text
src/Insighter.Host/wwwroot
```

Build it with:

```powershell
cd web
pnpm build
```

Then run the host:

```powershell
dotnet run --project src/Insighter.Host/Insighter.Host.csproj
```

The host will serve:

- SPA routes from `/`
- API routes from `/api/*`

## Configuration

Main runtime configuration lives in:

- [`src/Insighter.Host/appsettings.json`](./src/Insighter.Host/appsettings.json)

Important defaults:

- API URL: `http://127.0.0.1:5078`
- Queue capacity: `4096`
- Retention days: `30`
- Query max page size: `500`
- Default clipboard mode: `Preview`
- Default clipboard max length: `200`
- Default window title max length: `200`

## Development Commands

Frontend:

```powershell
cd web
pnpm typecheck
pnpm lint
pnpm build
```

Backend:

```powershell
dotnet build Insighter.slnx
dotnet test Insighter.slnx
```

## Privacy Notes

Insighter is intentionally local-first, but it still handles sensitive behavior data. In v1:

- Clipboard capture mode is configurable
- Window titles are truncated
- Process blacklist is supported for window redaction
- Redaction happens before persistence in the processing pipeline

You should still review settings before using it on real personal or work devices.

## Roadmap Direction

Planned / reserved architecture areas include:

- richer dashboard aggregations
- AI-assisted log analysis through controlled function boundaries
- macOS and Linux platform adapters
- broader observability and privacy controls

## License

This project is licensed under the [MIT License](./LICENSE).
