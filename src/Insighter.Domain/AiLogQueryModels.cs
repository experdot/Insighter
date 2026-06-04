namespace Insighter.Domain;

public sealed class AiLogQueryRequest
{
    public DateTimeOffset? FromUtc { get; set; }

    public DateTimeOffset? ToUtc { get; set; }

    public List<AiLogQueryFilter>? Filters { get; set; }

    public AiLogQuerySort? Sort { get; set; }

    public int? Limit { get; set; }

    public int? Offset { get; set; }

    public AiLogQuerySelect? Select { get; set; }

    public List<string>? GroupBy { get; set; }

    public string? TimeBucket { get; set; }

    public AiLogQueryContext? Context { get; set; }
}

public sealed class AiLogQueryFilter
{
    public string Field { get; set; } = string.Empty;

    public string Operator { get; set; } = "eq";

    public string? Value { get; set; }

    public List<string>? Values { get; set; }
}

public sealed class AiLogQuerySort
{
    public string Field { get; set; } = "occurredAtUtc";

    public string Direction { get; set; } = "desc";
}

public sealed class AiLogQuerySelect
{
    public List<string>? Fields { get; set; }

    public bool IncludeContent { get; set; }
}

public sealed class AiLogQueryContext
{
    public Guid AnchorEventId { get; set; }

    public int? BeforeCount { get; set; }

    public int? AfterCount { get; set; }
}

public sealed record AiLogQueryResult(
    string Mode,
    AiLogQueryMeta Meta,
    IReadOnlyList<AiLogQueryEventRow> Items,
    IReadOnlyList<AiLogQueryGroupRow> Groups,
    AiLogQueryEventRow? Anchor,
    IReadOnlyList<AiLogQueryEventRow> Before,
    IReadOnlyList<AiLogQueryEventRow> After);

public sealed record AiLogQueryMeta(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int TotalMatched,
    int ReturnedCount,
    bool ContentIncluded,
    IReadOnlyList<string> Warnings);

public sealed record AiLogQueryEventRow(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string EventType,
    string CollectorId,
    string Severity,
    string UserSessionId,
    string MachineId,
    string? Platform,
    IReadOnlyList<AiLogQueryFieldValue> Fields,
    string? ContentJson);

public sealed record AiLogQueryFieldValue(
    string Name,
    string? Value);

public sealed record AiLogQueryGroupRow(
    IReadOnlyList<AiLogQueryFieldValue> Key,
    int Count,
    DateTimeOffset? FirstOccurredAtUtc,
    DateTimeOffset? LastOccurredAtUtc);
