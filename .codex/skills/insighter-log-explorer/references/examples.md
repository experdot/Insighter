# Examples

## Group By Event Type Over The Last Day

```powershell
.\scripts\Group-InsighterLogs.ps1 `
  -TimeBucket hour `
  -GroupBy eventType `
  -Limit 24
```

## Find Foreground Window Events For VS Code

```powershell
.\scripts\Search-InsighterLogs.ps1 `
  -EventType Window.Activated `
  -Field data.processName `
  -Operator contains `
  -Value code `
  -Limit 20
```

## Look At Clipboard Activity With Raw Content

```powershell
.\scripts\Search-InsighterLogs.ps1 `
  -EventType Clipboard.Changed `
  -IncludeContent `
  -SelectField data.captureMode,data.preview,data.textLength `
  -Limit 10
```

## Inspect Context Around One Log Event

```powershell
.\scripts\Get-InsighterLogContext.ps1 `
  -AnchorEventId 11111111-2222-3333-4444-555555555555 `
  -BeforeCount 8 `
  -AfterCount 8
```

## Submit A Raw Query Document

```powershell
@'
{
  "filters": [
    { "field": "eventType", "operator": "eq", "value": "Window.Activated" },
    { "field": "data.processName", "operator": "contains", "value": "code" }
  ],
  "select": {
    "fields": ["platform", "data.processName", "data.windowTitle"],
    "includeContent": false
  },
  "limit": 25
}
'@ | Set-Content -Path .\query.json

.\scripts\Invoke-InsighterAiQuery.ps1 -RequestFile .\query.json
```
