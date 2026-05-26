using System.Text.Json.Nodes;

namespace Insighter.Domain;

public static class CollectorIds
{
    public const string SystemLifecycle = "SystemLifecycleCollector";
    public const string SessionState = "SessionStateCollector";
    public const string ForegroundWindow = "ForegroundWindowCollector";
    public const string Idle = "IdleCollector";
    public const string Clipboard = "ClipboardCollector";
    public const string ProcessLifecycle = "ProcessLifecycleCollector";
    public const string Power = "PowerCollector";
    public const string NetworkConnectivity = "NetworkConnectivityCollector";

    public static readonly IReadOnlyList<string> All =
    [
        SystemLifecycle,
        SessionState,
        ForegroundWindow,
        Idle,
        Clipboard,
        ProcessLifecycle,
        Power,
        NetworkConnectivity,
    ];
}

public static class EventTypes
{
    public const string SystemStartup = "System.Startup";
    public const string SystemShutdown = "System.Shutdown";
    public const string SystemSleep = "System.Sleep";
    public const string SystemResumed = "System.Resumed";
    public const string SessionLocked = "Session.Locked";
    public const string SessionUnlocked = "Session.Unlocked";
    public const string WindowActivated = "Window.Activated";
    public const string UserIdleStarted = "User.IdleStarted";
    public const string UserIdleEnded = "User.IdleEnded";
    public const string ClipboardChanged = "Clipboard.Changed";
    public const string ProcessStarted = "Process.Started";
    public const string ProcessExited = "Process.Exited";
    public const string PowerSourceChanged = "Power.SourceChanged";
    public const string PowerChargingStateChanged = "Power.ChargingStateChanged";
    public const string PowerBatteryLevelChanged = "Power.BatteryLevelChanged";
    public const string NetworkOnline = "Network.Online";
    public const string NetworkOffline = "Network.Offline";
    public const string NetworkActiveNetworkChanged = "Network.ActiveNetworkChanged";
}

public sealed record LogEvent(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string EventType,
    string CollectorId,
    EventSeverity Severity,
    string UserSessionId,
    string MachineId,
    string Content);

public sealed record CollectorConfig(
    string CollectorId,
    bool Enabled,
    long Version,
    DateTimeOffset UpdatedAtUtc,
    string SettingsJson);

public sealed record CollectorState(
    string CollectorId,
    CollectorStatus Status,
    DateTimeOffset? LastHeartbeatUtc,
    string? LastError,
    DateTimeOffset UpdatedAtUtc);

public sealed record PlatformCapability(
    string CapabilityId,
    CapabilitySupportLevel SupportLevel,
    string? Reason);

public sealed record AppSettingsSnapshot(
    string DatabasePath,
    string ApiUrls,
    int QueueCapacity,
    int RetentionDays,
    int QueryMaxPageSize,
    bool AutoStartCollectors);

public sealed record LogQueryFilter(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? EventType,
    string? CollectorId,
    EventSeverity? Severity,
    string? UserSessionId,
    string? MachineId,
    int Page,
    int PageSize);

public sealed record TimelineBucket(
    DateTimeOffset BucketStartUtc,
    DateTimeOffset BucketEndUtc,
    int TotalEvents,
    IReadOnlyDictionary<string, int> EventCountsByType);

public sealed record DashboardSummary(
    int TotalEvents,
    int WindowActivationCount,
    int ClipboardChangeCount,
    int IdlePeriodCount,
    double TotalIdleDurationSeconds,
    IReadOnlyDictionary<string, int> EventCountsByType);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record ForegroundWindowSnapshot(
    nint Handle,
    int ProcessId,
    string ProcessName,
    string WindowTitle);

public sealed record ClipboardSnapshot(
    uint SequenceNumber,
    bool HasText,
    string? Text);

public sealed record SystemLifecycleSignal(
    SystemLifecycleSignalKind Kind);

public sealed record ProcessLifecycleNotification(
    ProcessLifecycleEventKind Kind,
    int ProcessId,
    string ProcessName,
    int? ParentProcessId,
    string? ExecutablePath,
    int? ExitCode);

public sealed record PowerStatusSnapshot(
    PowerSourceKind PowerSource,
    PowerChargingState ChargingState,
    int? BatteryPercent);

public sealed record NetworkConnectivitySnapshot(
    bool IsOnline,
    string? InterfaceId,
    string? InterfaceName,
    string? InterfaceType);

public sealed record CollectorDetails(
    string CollectorId,
    string DisplayName,
    bool Enabled,
    CollectorStatus Status,
    CapabilitySupportLevel Capability,
    string? CapabilityReason,
    DateTimeOffset? LastHeartbeatUtc,
    string? LastError,
    long ConfigVersion,
    JsonObject? Settings);
