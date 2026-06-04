using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Insighter.Domain;

namespace Insighter.Application;

public sealed class AiLogQueryService : IAiLogQueryService
{
    private static readonly string[] TopLevelFields =
    [
        "id",
        "occurredAtUtc",
        "eventType",
        "collectorId",
        "severity",
        "userSessionId",
        "machineId",
        "platform",
    ];

    private static readonly HashSet<string> TopLevelFieldSet = TopLevelFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan DefaultLookback = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaxLookback = TimeSpan.FromDays(7);
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;
    private const int DefaultContextCount = 10;
    private const int MaxContextCount = 25;

    private readonly ILogEventRepository _repository;
    private readonly IClock _clock;

    public AiLogQueryService(ILogEventRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<AiLogQueryResult> ExecuteAsync(AiLogQueryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = NormalizeRequest(request);
        return normalized.Mode switch
        {
            QueryMode.Context => await ExecuteContextAsync(normalized, cancellationToken),
            QueryMode.Groups => await ExecuteGroupsAsync(normalized, cancellationToken),
            _ => await ExecuteRowsAsync(normalized, cancellationToken),
        };
    }

    private async Task<AiLogQueryResult> ExecuteRowsAsync(NormalizedQuery normalized, CancellationToken cancellationToken)
    {
        var documents = await LoadAndFilterDocumentsAsync(normalized.FromUtc, normalized.ToUtc, normalized.Filters, cancellationToken);
        var ordered = ApplySort(documents, normalized.SortDirection);
        var paged = ordered.Skip(normalized.Offset).Take(normalized.Limit).ToArray();

        return new AiLogQueryResult(
            "ROWS",
            new AiLogQueryMeta(normalized.FromUtc, normalized.ToUtc, documents.Count, paged.Length, normalized.IncludeContent, []),
            paged.Select(x => ToRow(x, normalized.SelectedFields, normalized.IncludeContent)).ToArray(),
            [],
            null,
            [],
            []);
    }

    private async Task<AiLogQueryResult> ExecuteGroupsAsync(NormalizedQuery normalized, CancellationToken cancellationToken)
    {
        var documents = await LoadAndFilterDocumentsAsync(normalized.FromUtc, normalized.ToUtc, normalized.Filters, cancellationToken);
        var grouped = documents
            .Select(document => new GroupCandidate(document, BuildGroupKey(document, normalized)))
            .GroupBy(x => CreateGroupToken(x.Key), StringComparer.Ordinal)
            .Select(group => new AiLogQueryGroupRow(
                group.First().Key,
                group.Count(),
                group.Min(item => item.Document.LogEvent.OccurredAtUtc),
                group.Max(item => item.Document.LogEvent.OccurredAtUtc)))
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.LastOccurredAtUtc)
            .Skip(normalized.Offset)
            .Take(normalized.Limit)
            .ToArray();

        return new AiLogQueryResult(
            "GROUPS",
            new AiLogQueryMeta(normalized.FromUtc, normalized.ToUtc, documents.Count, grouped.Length, false, []),
            [],
            grouped,
            null,
            [],
            []);
    }

    private async Task<AiLogQueryResult> ExecuteContextAsync(NormalizedQuery normalized, CancellationToken cancellationToken)
    {
        var anchorId = normalized.Context!.AnchorEventId;
        var anchor = await _repository.GetByIdAsync(anchorId, cancellationToken);
        if (anchor is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, "anchor_log_not_found", $"Log event '{anchorId}' was not found.");
        }

        var anchorRangeStart = anchor.OccurredAtUtc - MaxLookback;
        var anchorRangeEnd = anchor.OccurredAtUtc + MaxLookback;
        var fromUtc = normalized.FromUtc > anchorRangeStart ? normalized.FromUtc : anchorRangeStart;
        var toUtc = normalized.ToUtc < anchorRangeEnd ? normalized.ToUtc : anchorRangeEnd;
        var documents = await LoadDocumentsAsync(fromUtc, toUtc, cancellationToken);
        var ordered = documents.OrderBy(x => x.LogEvent.OccurredAtUtc).ThenBy(x => x.LogEvent.Id).ToArray();
        var anchorIndex = Array.FindIndex(ordered, x => x.LogEvent.Id == anchorId);

        if (anchorIndex < 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "anchor_out_of_range", "The requested anchor event is outside the effective context range.");
        }

        var before = ordered
            .Take(anchorIndex)
            .TakeLast(normalized.Context.BeforeCount)
            .Select(x => ToRow(x, normalized.SelectedFields, normalized.IncludeContent))
            .ToArray();

        var after = ordered
            .Skip(anchorIndex + 1)
            .Take(normalized.Context.AfterCount)
            .Select(x => ToRow(x, normalized.SelectedFields, normalized.IncludeContent))
            .ToArray();

        return new AiLogQueryResult(
            "CONTEXT",
            new AiLogQueryMeta(fromUtc, toUtc, before.Length + after.Length + 1, before.Length + after.Length + 1, normalized.IncludeContent, []),
            [],
            [],
            ToRow(ordered[anchorIndex], normalized.SelectedFields, normalized.IncludeContent),
            before,
            after);
    }

    private async Task<IReadOnlyList<ParsedLogEvent>> LoadAndFilterDocumentsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IReadOnlyList<NormalizedFilter> filters,
        CancellationToken cancellationToken)
    {
        var documents = await LoadDocumentsAsync(fromUtc, toUtc, cancellationToken);
        return documents.Where(document => filters.All(filter => MatchesFilter(document, filter))).ToArray();
    }

    private async Task<IReadOnlyList<ParsedLogEvent>> LoadDocumentsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var events = await _repository.GetRangeAsync(fromUtc, toUtc, cancellationToken);
        return events.Select(ParseDocument).ToArray();
    }

    private static ParsedLogEvent ParseDocument(LogEvent logEvent)
    {
        JsonObject? root = null;
        JsonObject? data = null;
        string? platform = null;

        try
        {
            root = JsonNode.Parse(logEvent.Content) as JsonObject;
            platform = root?["platform"]?.GetValue<string>();
            data = root?["data"] as JsonObject;
        }
        catch (Exception)
        {
        }

        return new ParsedLogEvent(logEvent, platform, data, root);
    }

    private NormalizedQuery NormalizeRequest(AiLogQueryRequest request)
    {
        var filters = NormalizeFilters(request.Filters);
        var groupFields = NormalizeFieldList(request.GroupBy, "groupBy");
        var selectedFields = NormalizeFieldList(request.Select?.Fields, "select.fields");
        var includeContent = request.Select?.IncludeContent ?? false;
        var sortDirection = NormalizeSortDirection(request.Sort);
        var timeBucket = NormalizeTimeBucket(request.TimeBucket);
        var context = NormalizeContext(request.Context);

        if (context is not null)
        {
            if (filters.Count > 0 || groupFields.Count > 0 || timeBucket is not null)
            {
                throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", "Context queries cannot be combined with filters, grouping, or time buckets.");
            }
        }

        var mode = context is not null
            ? QueryMode.Context
            : groupFields.Count > 0 || timeBucket is not null
                ? QueryMode.Groups
                : QueryMode.Rows;

        if (mode == QueryMode.Groups && includeContent)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", "Grouped queries cannot include raw event content.");
        }

        var (fromUtc, toUtc) = mode == QueryMode.Context
            ? NormalizeContextRange(request.FromUtc, request.ToUtc)
            : NormalizeWindow(request.FromUtc, request.ToUtc);

        var limit = ClampPositive(request.Limit, DefaultLimit, MaxLimit);
        var offset = Math.Max(0, request.Offset ?? 0);

        return new NormalizedQuery(
            mode,
            fromUtc,
            toUtc,
            filters,
            groupFields,
            selectedFields,
            includeContent,
            sortDirection,
            timeBucket,
            context,
            limit,
            offset);
    }

    private (DateTimeOffset FromUtc, DateTimeOffset ToUtc) NormalizeWindow(DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        var effectiveToUtc = toUtc ?? _clock.UtcNow;
        var effectiveFromUtc = fromUtc ?? effectiveToUtc - DefaultLookback;

        if (effectiveFromUtc > effectiveToUtc)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_time_range", "fromUtc must be earlier than toUtc.");
        }

        if (effectiveToUtc - effectiveFromUtc > MaxLookback)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "time_range_too_large", "AI queries are limited to a seven day range.");
        }

        return (effectiveFromUtc, effectiveToUtc);
    }

    private static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) NormalizeContextRange(DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_time_range", "fromUtc must be earlier than toUtc.");
        }

        return (fromUtc ?? DateTimeOffset.MinValue, toUtc ?? DateTimeOffset.MaxValue);
    }

    private static IReadOnlyList<NormalizedFilter> NormalizeFilters(IReadOnlyList<AiLogQueryFilter>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return [];
        }

        return filters.Select(NormalizeFilter).ToArray();
    }

    private static NormalizedFilter NormalizeFilter(AiLogQueryFilter filter)
    {
        var field = NormalizeFieldName(filter.Field, "filters.field");
        var op = NormalizeOperator(filter.Operator);
        var values = (filter.Values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        var value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim();

        if (op == "in")
        {
            if (values.Length == 0 && value is not null)
            {
                values = [value];
            }

            if (values.Length == 0)
            {
                throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", "The 'in' operator requires at least one value.");
            }
        }
        else if (op != "exists" && value is null)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", $"The '{op}' operator requires a value.");
        }

        return new NormalizedFilter(field, op, value, values);
    }

    private static string NormalizeFieldName(string? fieldName, string source)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query_field", $"{source} must not be empty.");
        }

        var trimmed = fieldName.Trim();
        if (TopLevelFieldSet.Contains(trimmed))
        {
            return TopLevelFields.First(field => string.Equals(field, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        if (!trimmed.StartsWith("data.", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query_field", $"Unsupported field '{trimmed}'.");
        }

        var remainder = trimmed[5..].Trim();
        if (string.IsNullOrWhiteSpace(remainder) || remainder.Contains('.', StringComparison.Ordinal))
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query_field", $"Unsupported field '{trimmed}'. Only one-level data.* fields are allowed.");
        }

        return $"data.{remainder}";
    }

    private static IReadOnlyList<string> NormalizeFieldList(IReadOnlyList<string>? values, string source)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return values
            .Select(value => NormalizeFieldName(value, source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeSortDirection(AiLogQuerySort? sort)
    {
        if (sort is null)
        {
            return "desc";
        }

        var field = NormalizeFieldName(sort.Field, "sort.field");
        if (!string.Equals(field, "occurredAtUtc", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", "Only sorting by occurredAtUtc is supported.");
        }

        var direction = string.IsNullOrWhiteSpace(sort.Direction) ? "desc" : sort.Direction.Trim().ToLowerInvariant();
        return direction switch
        {
            "asc" => "asc",
            "desc" => "desc",
            _ => throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", $"Unsupported sort direction '{sort.Direction}'."),
        };
    }

    private static string? NormalizeTimeBucket(string? timeBucket)
    {
        if (string.IsNullOrWhiteSpace(timeBucket))
        {
            return null;
        }

        var normalized = timeBucket.Trim().ToLowerInvariant();
        return normalized switch
        {
            "hour" => "hour",
            "day" => "day",
            _ => throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", $"Unsupported timeBucket '{timeBucket}'."),
        };
    }

    private static NormalizedContext? NormalizeContext(AiLogQueryContext? context)
    {
        if (context is null)
        {
            return null;
        }

        if (context.AnchorEventId == Guid.Empty)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", "context.anchorEventId must not be empty.");
        }

        return new NormalizedContext(
            context.AnchorEventId,
            ClampPositive(context.BeforeCount, DefaultContextCount, MaxContextCount),
            ClampPositive(context.AfterCount, DefaultContextCount, MaxContextCount));
    }

    private static int ClampPositive(int? value, int fallback, int maxValue)
    {
        var effective = value ?? fallback;
        if (effective <= 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", "Limit values must be greater than zero.");
        }

        return Math.Min(effective, maxValue);
    }

    private static string NormalizeOperator(string? op)
    {
        var normalized = string.IsNullOrWhiteSpace(op) ? "eq" : op.Trim().ToLowerInvariant();
        return normalized switch
        {
            "eq" or "in" or "exists" or "contains" or "gt" or "gte" or "lt" or "lte" => normalized,
            _ => throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query_operator", $"Unsupported operator '{op}'."),
        };
    }

    private static IEnumerable<ParsedLogEvent> ApplySort(IEnumerable<ParsedLogEvent> documents, string direction)
        => string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase)
            ? documents.OrderBy(x => x.LogEvent.OccurredAtUtc).ThenBy(x => x.LogEvent.Id)
            : documents.OrderByDescending(x => x.LogEvent.OccurredAtUtc).ThenByDescending(x => x.LogEvent.Id);

    private static IReadOnlyList<AiLogQueryFieldValue> BuildGroupKey(ParsedLogEvent document, NormalizedQuery normalized)
    {
        var key = new List<AiLogQueryFieldValue>();

        if (normalized.TimeBucket is not null)
        {
            key.Add(new AiLogQueryFieldValue("timeBucketStart", Align(document.LogEvent.OccurredAtUtc, normalized.TimeBucket).ToString("O")));
        }

        foreach (var field in normalized.GroupFields)
        {
            key.Add(new AiLogQueryFieldValue(field, ResolveFieldValue(document, field)?.FormattedValue));
        }

        return key;
    }

    private static string CreateGroupToken(IReadOnlyList<AiLogQueryFieldValue> key)
        => string.Join("\u001F", key.Select(item => $"{item.Name}={item.Value ?? "<null>"}"));

    private static DateTimeOffset Align(DateTimeOffset value, string timeBucket)
    {
        var utc = value.ToUniversalTime();
        return string.Equals(timeBucket, "day", StringComparison.OrdinalIgnoreCase)
            ? new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    private static AiLogQueryEventRow ToRow(ParsedLogEvent document, IReadOnlyList<string> selectedFields, bool includeContent)
    {
        var fields = selectedFields.Count > 0
            ? selectedFields.Select(field => new AiLogQueryFieldValue(field, ResolveFieldValue(document, field)?.FormattedValue)).ToArray()
            : BuildDefaultFields(document);

        return new AiLogQueryEventRow(
            document.LogEvent.Id,
            document.LogEvent.OccurredAtUtc,
            document.LogEvent.EventType,
            document.LogEvent.CollectorId,
            document.LogEvent.Severity.ToString(),
            document.LogEvent.UserSessionId,
            document.LogEvent.MachineId,
            document.Platform,
            fields,
            includeContent ? document.LogEvent.Content : null);
    }

    private static IReadOnlyList<AiLogQueryFieldValue> BuildDefaultFields(ParsedLogEvent document)
    {
        var fields = new List<AiLogQueryFieldValue>();
        if (!string.IsNullOrWhiteSpace(document.Platform))
        {
            fields.Add(new AiLogQueryFieldValue("platform", document.Platform));
        }

        if (document.Data is not null)
        {
            foreach (var property in document.Data.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                fields.Add(new AiLogQueryFieldValue($"data.{property.Key}", ResolveFieldValue(document, $"data.{property.Key}")?.FormattedValue));
            }
        }

        return fields;
    }

    private static bool MatchesFilter(ParsedLogEvent document, NormalizedFilter filter)
    {
        var resolved = ResolveFieldValue(document, filter.Field);
        if (filter.Operator == "exists")
        {
            var shouldExist = ParseBoolean(filter.Value, true);
            return shouldExist ? resolved is not null : resolved is null;
        }

        if (resolved is null)
        {
            return false;
        }

        return filter.Operator switch
        {
            "eq" => MatchesEquality(resolved, filter.Value!),
            "in" => filter.Values.Any(candidate => MatchesEquality(resolved, candidate)),
            "contains" => resolved.Kind == ResolvedFieldKind.String &&
                resolved.FormattedValue?.Contains(filter.Value!, StringComparison.OrdinalIgnoreCase) == true,
            "gt" => CompareOrdered(resolved, filter.Value!, filter.Operator) > 0,
            "gte" => CompareOrdered(resolved, filter.Value!, filter.Operator) >= 0,
            "lt" => CompareOrdered(resolved, filter.Value!, filter.Operator) < 0,
            "lte" => CompareOrdered(resolved, filter.Value!, filter.Operator) <= 0,
            _ => false,
        };
    }

    private static bool MatchesEquality(ResolvedFieldValue resolved, string expected)
        => resolved.Kind switch
        {
            ResolvedFieldKind.Boolean => string.Equals(resolved.FormattedValue, ParseBoolean(expected, false).ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase),
            ResolvedFieldKind.DateTime => string.Equals(
                resolved.FormattedValue,
                ParseDateTime(expected, "eq").ToString("O"),
                StringComparison.OrdinalIgnoreCase),
            ResolvedFieldKind.Number => NumbersEqual(resolved.NumericValue, ParseNumber(expected, "eq")),
            _ => string.Equals(resolved.FormattedValue, expected, StringComparison.OrdinalIgnoreCase),
        };

    private static int CompareOrdered(ResolvedFieldValue resolved, string expected, string op)
    {
        return resolved.Kind switch
        {
            ResolvedFieldKind.DateTime => resolved.DateTimeValue.CompareTo(ParseDateTime(expected, op)),
            ResolvedFieldKind.Number => resolved.NumericValue.CompareTo(ParseNumber(expected, op)),
            _ => throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query_operator", $"The '{op}' operator is only supported on numeric and datetime fields."),
        };
    }

    private static bool NumbersEqual(double left, double right)
        => Math.Abs(left - right) < 0.000001d;

    private static bool ParseBoolean(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", $"'{value}' is not a valid boolean value.");
    }

    private static double ParseNumber(string value, string op)
    {
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", $"'{value}' is not a valid numeric value for '{op}'.");
    }

    private static DateTimeOffset ParseDateTime(string value, string op)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        throw new ApiException(HttpStatusCode.BadRequest, "invalid_ai_query", $"'{value}' is not a valid datetime value for '{op}'.");
    }

    private static ResolvedFieldValue? ResolveFieldValue(ParsedLogEvent document, string field)
    {
        return field switch
        {
            "id" => ResolvedFieldValue.FromString(document.LogEvent.Id.ToString()),
            "occurredAtUtc" => ResolvedFieldValue.FromDateTime(document.LogEvent.OccurredAtUtc),
            "eventType" => ResolvedFieldValue.FromString(document.LogEvent.EventType),
            "collectorId" => ResolvedFieldValue.FromString(document.LogEvent.CollectorId),
            "severity" => ResolvedFieldValue.FromString(document.LogEvent.Severity.ToString()),
            "userSessionId" => ResolvedFieldValue.FromString(document.LogEvent.UserSessionId),
            "machineId" => ResolvedFieldValue.FromString(document.LogEvent.MachineId),
            "platform" => ResolvedFieldValue.FromString(document.Platform),
            _ => ResolveDataField(document, field),
        };
    }

    private static ResolvedFieldValue? ResolveDataField(ParsedLogEvent document, string field)
    {
        if (document.Data is null || !field.StartsWith("data.", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var key = field[5..];
        return document.Data.TryGetPropertyValue(key, out var node)
            ? ResolvedFieldValue.FromNode(node)
            : null;
    }

    private enum QueryMode
    {
        Rows,
        Groups,
        Context,
    }

    private sealed record NormalizedQuery(
        QueryMode Mode,
        DateTimeOffset FromUtc,
        DateTimeOffset ToUtc,
        IReadOnlyList<NormalizedFilter> Filters,
        IReadOnlyList<string> GroupFields,
        IReadOnlyList<string> SelectedFields,
        bool IncludeContent,
        string SortDirection,
        string? TimeBucket,
        NormalizedContext? Context,
        int Limit,
        int Offset);

    private sealed record NormalizedFilter(
        string Field,
        string Operator,
        string? Value,
        IReadOnlyList<string> Values);

    private sealed record NormalizedContext(
        Guid AnchorEventId,
        int BeforeCount,
        int AfterCount);

    private sealed record ParsedLogEvent(
        LogEvent LogEvent,
        string? Platform,
        JsonObject? Data,
        JsonObject? Root);

    private sealed record GroupCandidate(
        ParsedLogEvent Document,
        IReadOnlyList<AiLogQueryFieldValue> Key);

    private enum ResolvedFieldKind
    {
        String,
        Number,
        Boolean,
        DateTime,
    }

    private sealed record ResolvedFieldValue(
        ResolvedFieldKind Kind,
        string? FormattedValue,
        double NumericValue,
        DateTimeOffset DateTimeValue)
    {
        public static ResolvedFieldValue FromString(string? value)
            => new(ResolvedFieldKind.String, value, 0, default);

        public static ResolvedFieldValue FromBoolean(bool value)
            => new(ResolvedFieldKind.Boolean, value.ToString().ToLowerInvariant(), 0, default);

        public static ResolvedFieldValue FromNumber(double value)
            => new(ResolvedFieldKind.Number, value.ToString(CultureInfo.InvariantCulture), value, default);

        public static ResolvedFieldValue FromDateTime(DateTimeOffset value)
            => new(ResolvedFieldKind.DateTime, value.ToUniversalTime().ToString("O"), 0, value.ToUniversalTime());

        public static ResolvedFieldValue? FromNode(JsonNode? node)
        {
            if (node is null)
            {
                return null;
            }

            if (node is JsonValue valueNode)
            {
                if (valueNode.TryGetValue<string>(out var textValue))
                {
                    return FromString(textValue);
                }

                if (valueNode.TryGetValue<bool>(out var boolValue))
                {
                    return FromBoolean(boolValue);
                }

                if (valueNode.TryGetValue<int>(out var intValue))
                {
                    return FromNumber(intValue);
                }

                if (valueNode.TryGetValue<long>(out var longValue))
                {
                    return FromNumber(longValue);
                }

                if (valueNode.TryGetValue<double>(out var doubleValue))
                {
                    return FromNumber(doubleValue);
                }

                if (valueNode.TryGetValue<decimal>(out var decimalValue))
                {
                    return FromNumber((double)decimalValue);
                }
            }

            return FromString(node.ToJsonString(JsonHelpers.SerializerOptions));
        }
    }
}
