---
name: insighter-log-explorer
description: Explore local Insighter logs through the repository's bundled PowerShell GraphQL wrappers. Use when Codex needs to investigate personal system activity captured by Insighter, search structured log events, group activity over time or by app/process, inspect event context around one anchor log, or analyze clipboard, window, power, network, session, and idle events without writing raw curl or manual GraphQL requests.
---

# Insighter Log Explorer

## Overview

Use the bundled PowerShell wrappers as the only query surface for Insighter log exploration. They handle base URL discovery, GraphQL transport, stable JSON output, and common search/group/context workflows so you do not have to handcraft HTTP requests.

## Workflow

1. Start with `Group-InsighterLogs.ps1` to measure event volume by time bucket, event type, or process.
2. Narrow the investigation with `Search-InsighterLogs.ps1` using a short time range and 1-2 filters.
3. When one event looks important, fetch surrounding activity with `Get-InsighterLogContext.ps1`.
4. Use `Invoke-InsighterAiQuery.ps1` only when the friendly wrappers are too narrow.
5. Use `Invoke-InsighterGraphQL.ps1` only for schema-level debugging or transport troubleshooting.

## Scripts

- Use `scripts/Search-InsighterLogs.ps1` for row-level event search with top-level filters and one extra custom field filter.
- Use `scripts/Group-InsighterLogs.ps1` for timeline-style and grouped summaries.
- Use `scripts/Get-InsighterLogContext.ps1` for before/after context around an anchor event ID.
- Use `scripts/Invoke-InsighterAiQuery.ps1` when you need to submit a raw `AiLogQueryRequest` JSON document.
- Use `scripts/Invoke-InsighterGraphQL.ps1` only when you need a custom GraphQL query.
- Use the `.cmd` shims when Windows execution policy blocks direct `.ps1` execution.

## Guardrails

- Prefer narrow time ranges. The backend defaults to the last 24 hours and caps one request to 7 days.
- Prefer grouped exploration before row dumps.
- Do not handwrite `curl`, `Invoke-WebRequest`, or ad-hoc GraphQL bodies when the bundled wrappers cover the task.
- Do not guess `data.*` fields. Read `references/fields.md` first.
- Treat `windowTitle`, `preview`, and `text` as potentially sensitive. Only request full content when it is necessary.

## References

- Read `references/schema.md` for the GraphQL query shape and request model.
- Read `references/fields.md` for supported `data.*` fields mapped from current collectors.
- Read `references/examples.md` for ready-to-run wrapper commands.
