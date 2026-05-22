namespace Insighter.Infrastructure.Persistence;

internal sealed class CollectorConfigRow
{
    public required string CollectorId { get; init; }
    public required long Enabled { get; init; }
    public required long Version { get; init; }
    public required long UpdatedAtUtc { get; init; }
    public required string SettingsJson { get; init; }
}

internal sealed class CollectorStateRow
{
    public required string CollectorId { get; init; }
    public required int Status { get; init; }
    public long? LastHeartbeatUtc { get; init; }
    public string? LastError { get; init; }
    public required long UpdatedAtUtc { get; init; }
}

internal sealed class LogEventRow
{
    public required string Id { get; init; }
    public required long OccurredAtUtc { get; init; }
    public required string EventType { get; init; }
    public required string CollectorId { get; init; }
    public required int Severity { get; init; }
    public required string UserSessionId { get; init; }
    public required string MachineId { get; init; }
    public required string Content { get; init; }
}

internal sealed class AppSettingsRow
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}
