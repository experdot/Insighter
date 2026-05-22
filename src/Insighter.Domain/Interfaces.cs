namespace Insighter.Domain;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IEventSink
{
    ValueTask WriteAsync(LogEvent logEvent, CancellationToken cancellationToken);
}

public interface ICollector
{
    string CollectorId { get; }

    string DisplayName { get; }

    Task StartAsync(CollectorConfig config, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task UpdateConfigAsync(CollectorConfig config, CancellationToken cancellationToken);

    ValueTask<CollectorState> GetStatusAsync(CancellationToken cancellationToken);
}

public interface ICollectorManager
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task StopAllAsync(CancellationToken cancellationToken);

    Task MonitorAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CollectorDetails>> GetCollectorsAsync(CancellationToken cancellationToken);

    Task<CollectorDetails> GetCollectorAsync(string collectorId, CancellationToken cancellationToken);

    Task StartCollectorAsync(string collectorId, CancellationToken cancellationToken);

    Task StopCollectorAsync(string collectorId, CancellationToken cancellationToken);

    Task<CollectorDetails> UpdateCollectorConfigAsync(string collectorId, bool enabled, string settingsJson, CancellationToken cancellationToken);
}

public interface ILogEventRepository
{
    Task InsertBatchAsync(IReadOnlyCollection<LogEvent> events, CancellationToken cancellationToken);

    Task<PagedResult<LogEvent>> SearchAsync(LogQueryFilter filter, CancellationToken cancellationToken);

    Task<LogEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<LogEvent>> GetRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
}

public interface ICollectorConfigRepository
{
    Task<IReadOnlyList<CollectorConfig>> GetAllAsync(CancellationToken cancellationToken);

    Task<CollectorConfig?> GetAsync(string collectorId, CancellationToken cancellationToken);

    Task UpsertAsync(CollectorConfig config, CancellationToken cancellationToken);
}

public interface ICollectorStateRepository
{
    Task<IReadOnlyList<CollectorState>> GetAllAsync(CancellationToken cancellationToken);

    Task<CollectorState?> GetAsync(string collectorId, CancellationToken cancellationToken);

    Task UpsertAsync(CollectorState state, CancellationToken cancellationToken);
}

public interface IAppSettingsRepository
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken);

    Task UpsertAsync(string key, string value, CancellationToken cancellationToken);

    Task UpsertManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
}

public interface ILogQueryService
{
    Task<PagedResult<LogEvent>> SearchAsync(LogQueryFilter filter, CancellationToken cancellationToken);

    Task<LogEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

public interface ILogAggregationService
{
    Task<DashboardSummary> GetSummaryAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<TimelineBucket>> GetTimelineAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, string bucket, CancellationToken cancellationToken);
}

public interface IAiLogQueryService
{
    Task<IReadOnlyList<LogEvent>> SearchLogsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int maxResults,
        string? eventType,
        CancellationToken cancellationToken);
}

public interface ISettingsService
{
    Task<AppSettingsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<AppSettingsSnapshot> UpdateAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
}

public interface IContentValidator
{
    void Validate(LogEvent logEvent);
}

public interface IRedactionService
{
    Task<LogEvent> RedactAsync(LogEvent logEvent, CancellationToken cancellationToken);
}

public interface IMachineContextProvider
{
    string GetMachineId();

    string GetPlatform();
}

public interface IUserSessionContextProvider
{
    string GetSessionId();
}

public interface IForegroundWindowMonitor
{
    ValueTask<ForegroundWindowSnapshot?> GetCurrentAsync(CancellationToken cancellationToken);
}

public interface IClipboardMonitor
{
    ValueTask<ClipboardSnapshot?> GetCurrentAsync(CancellationToken cancellationToken);
}

public interface IIdleMonitor
{
    ValueTask<TimeSpan> GetIdleTimeAsync(CancellationToken cancellationToken);
}

public interface ISessionStateMonitor
{
    ValueTask<SessionLockState> GetCurrentAsync(CancellationToken cancellationToken);
}

public interface ISystemLifecycleMonitor
{
    ValueTask<SystemLifecycleSignal> WaitForNextAsync(CancellationToken cancellationToken);
}

public interface IProcessLifecycleMonitor
{
    ValueTask<ProcessLifecycleNotification> WaitForNextAsync(CancellationToken cancellationToken);
}

public interface IPowerStatusMonitor
{
    ValueTask<PowerStatusSnapshot> GetCurrentAsync(CancellationToken cancellationToken);

    ValueTask WaitForChangeAsync(CancellationToken cancellationToken);
}

public interface INetworkConnectivityMonitor
{
    ValueTask<NetworkConnectivitySnapshot> GetCurrentAsync(CancellationToken cancellationToken);

    ValueTask WaitForChangeAsync(CancellationToken cancellationToken);
}

public interface IPlatformCapabilityProvider
{
    IReadOnlyCollection<PlatformCapability> GetCapabilities();
}
