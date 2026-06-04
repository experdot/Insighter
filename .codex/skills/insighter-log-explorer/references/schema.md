# GraphQL Shape

All wrappers target `POST /graphql` and submit the same query document:

```graphql
query InsighterAiQuery($input: AiLogQueryRequestInput!) {
  aiQuery(input: $input) {
    mode
    meta {
      fromUtc
      toUtc
      totalMatched
      returnedCount
      contentIncluded
      warnings
    }
    items {
      id
      occurredAtUtc
      eventType
      collectorId
      severity
      userSessionId
      machineId
      platform
      fields {
        name
        value
      }
      contentJson
    }
    groups {
      key {
        name
        value
      }
      count
      firstOccurredAtUtc
      lastOccurredAtUtc
    }
    anchor {
      id
      occurredAtUtc
      eventType
      collectorId
      severity
      userSessionId
      machineId
      platform
      fields {
        name
        value
      }
      contentJson
    }
    before {
      id
      occurredAtUtc
      eventType
      collectorId
      severity
      userSessionId
      machineId
      platform
      fields {
        name
        value
      }
      contentJson
    }
    after {
      id
      occurredAtUtc
      eventType
      collectorId
      severity
      userSessionId
      machineId
      platform
      fields {
        name
        value
      }
      contentJson
    }
  }
}
```

## Request Model

- `fromUtc`, `toUtc`: optional ISO-8601 UTC timestamps.
- `filters`: array of `{ field, operator, value, values }`.
- `sort`: only `{ field: "occurredAtUtc", direction: "asc"|"desc" }`.
- `limit`, `offset`: row/group paging controls.
- `select`: `{ fields, includeContent }`.
- `groupBy`: array of fields.
- `timeBucket`: `"hour"` or `"day"`.
- `context`: `{ anchorEventId, beforeCount, afterCount }`.

## Operators

- `eq`
- `in`
- `exists`
- `contains`
- `gt`
- `gte`
- `lt`
- `lte`

## Result Modes

- `ROWS`: `items` is populated.
- `GROUPS`: `groups` is populated.
- `CONTEXT`: `anchor`, `before`, and `after` are populated.
