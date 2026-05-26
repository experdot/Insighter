using System.Text.Json.Nodes;

namespace Insighter.Contracts;

public sealed record ErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details,
    string TraceId);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record CollectorConfigRequest(
    bool Enabled,
    long Version,
    JsonObject? Settings);

public sealed record CollectorResponse(
    string CollectorId,
    string DisplayName,
    bool Enabled,
    string Status,
    string Capability,
    string? CapabilityReason,
    DateTimeOffset? LastHeartbeatUtc,
    string? LastError,
    long ConfigVersion,
    JsonObject? Settings);

public sealed record LogEventResponse(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string EventType,
    string CollectorId,
    string Severity,
    string UserSessionId,
    string MachineId,
    JsonNode Content);

public sealed record DashboardSummaryResponse(
    int TotalEvents,
    int WindowActivationCount,
    int ClipboardChangeCount,
    int IdlePeriodCount,
    double TotalIdleDurationSeconds,
    IReadOnlyDictionary<string, int> EventCountsByType);

public sealed record TimelineBucketResponse(
    DateTimeOffset BucketStartUtc,
    DateTimeOffset BucketEndUtc,
    int TotalEvents,
    IReadOnlyDictionary<string, int> EventCountsByType);

public sealed record PlatformCapabilityResponse(
    string CapabilityId,
    string SupportLevel,
    string? Reason);

public sealed record SettingsResponse(
    string DatabasePath,
    string ApiUrls,
    int QueueCapacity,
    int RetentionDays,
    int QueryMaxPageSize,
    bool AutoStartCollectors);

public sealed record SettingsUpdateRequest(
    int? RetentionDays);
