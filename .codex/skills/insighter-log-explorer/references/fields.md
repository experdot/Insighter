# Supported Fields

## Top-Level Fields

- `id`
- `occurredAtUtc`
- `eventType`
- `collectorId`
- `severity`
- `userSessionId`
- `machineId`
- `platform`

## `data.*` Fields By Collector

### `SystemLifecycleCollector`

- `data.source`
- `data.signal`

### `SessionStateCollector`

- `data.state`

### `ForegroundWindowCollector`

- `data.processId`
- `data.processName`
- `data.windowTitle`
- `data.windowTitleHash`
- `data.redacted`

### `IdleCollector`

- `data.idleThresholdSeconds`
- `data.idleSeconds`

### `ClipboardCollector`

- `data.sequenceNumber`
- `data.hasText`
- `data.captureMode`
- `data.textLength`
- `data.textHash`
- `data.preview`
- `data.text`

### `PowerCollector`

- `data.previousPowerSource`
- `data.currentPowerSource`
- `data.chargingState`
- `data.batteryPercent`
- `data.stepPercent`
- `data.previousBatteryPercent`
- `data.currentBatteryPercent`
- `data.powerSource`

### `NetworkConnectivityCollector`

- `data.isOnline`
- `data.interfaceName`
- `data.interfaceType`
- `data.previousInterfaceName`
- `data.previousInterfaceType`
- `data.currentInterfaceName`
- `data.currentInterfaceType`

## Notes

- Only one-level `data.*` fields are supported.
- Do not use `content.data.*`, JSONPath, or nested object paths.
- `contains` is best for string fields such as `data.processName` or `data.windowTitle`.
