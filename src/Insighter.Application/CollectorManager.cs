using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Nodes;
using Insighter.Domain;
using Microsoft.Extensions.Logging;

namespace Insighter.Application;

public sealed class CollectorManager : ICollectorManager
{
    private static readonly TimeSpan[] RetrySchedule =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
    ];

    private readonly ConcurrentDictionary<string, CollectorRuntime> _runtimes;
    private readonly ICollectorConfigRepository _configRepository;
    private readonly ICollectorStateRepository _stateRepository;
    private readonly ISettingsService _settingsService;
    private readonly IPlatformCapabilityProvider _capabilityProvider;
    private readonly IClock _clock;
    private readonly ILogger<CollectorManager> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public CollectorManager(
        IEnumerable<ICollector> collectors,
        ICollectorConfigRepository configRepository,
        ICollectorStateRepository stateRepository,
        ISettingsService settingsService,
        IPlatformCapabilityProvider capabilityProvider,
        IClock clock,
        ILogger<CollectorManager> logger)
    {
        _configRepository = configRepository;
        _stateRepository = stateRepository;
        _settingsService = settingsService;
        _capabilityProvider = capabilityProvider;
        _clock = clock;
        _logger = logger;
        _runtimes = new ConcurrentDictionary<string, CollectorRuntime>(
            collectors.ToDictionary(
                collector => collector.CollectorId,
                collector => new CollectorRuntime(collector)));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var settings = await _settingsService.GetSnapshotAsync(cancellationToken);
            var configs = (await _configRepository.GetAllAsync(cancellationToken)).ToDictionary(x => x.CollectorId, StringComparer.OrdinalIgnoreCase);
            var states = (await _stateRepository.GetAllAsync(cancellationToken)).ToDictionary(x => x.CollectorId, StringComparer.OrdinalIgnoreCase);
            var capabilities = _capabilityProvider.GetCapabilities().ToDictionary(x => x.CapabilityId, StringComparer.OrdinalIgnoreCase);

            foreach (var runtime in _runtimes.Values)
            {
                runtime.Capability = capabilities.GetValueOrDefault(runtime.Collector.CollectorId) ??
                    new PlatformCapability(runtime.Collector.CollectorId, CapabilitySupportLevel.Supported, null);

                runtime.Config = configs.GetValueOrDefault(runtime.Collector.CollectorId) ??
                    new CollectorConfig(
                        runtime.Collector.CollectorId,
                        true,
                        1,
                        _clock.UtcNow,
                        CollectorConfigDefaults.BuildSettingsJson(runtime.Collector.CollectorId, settings));

                runtime.State = states.GetValueOrDefault(runtime.Collector.CollectorId) ??
                    new CollectorState(runtime.Collector.CollectorId, CollectorStatus.Disabled, null, null, _clock.UtcNow);

                await _configRepository.UpsertAsync(runtime.Config, cancellationToken);
                await _stateRepository.UpsertAsync(runtime.State, cancellationToken);
            }

            _initialized = true;

            if (settings.AutoStartCollectors)
            {
                foreach (var runtime in _runtimes.Values.OrderBy(x => x.Collector.CollectorId))
                {
                    if (runtime.Config.Enabled)
                    {
                        await StartCollectorInternalAsync(runtime, cancellationToken);
                    }
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        foreach (var runtime in _runtimes.Values.OrderBy(x => x.Collector.CollectorId))
        {
            await StopCollectorInternalAsync(runtime, cancellationToken, persistDisabledState: false);
        }
    }

    public async Task MonitorAsync(CancellationToken cancellationToken)
    {
        foreach (var runtime in _runtimes.Values.OrderBy(x => x.Collector.CollectorId))
        {
            var status = await runtime.Collector.GetStatusAsync(cancellationToken);
            runtime.State = status;
            await _stateRepository.UpsertAsync(status, cancellationToken);

            if (!runtime.Config.Enabled || runtime.Capability.SupportLevel == CapabilitySupportLevel.Unsupported)
            {
                continue;
            }

            if (status.Status == CollectorStatus.Faulted && runtime.NextRetryAtUtc <= _clock.UtcNow)
            {
                runtime.RetryCount++;
                runtime.NextRetryAtUtc = _clock.UtcNow.Add(RetrySchedule[Math.Min(runtime.RetryCount - 1, RetrySchedule.Length - 1)]);
                _logger.LogWarning("Restarting faulted collector {CollectorId}.", runtime.Collector.CollectorId);
                await StopCollectorInternalAsync(runtime, cancellationToken, persistDisabledState: false);
                await StartCollectorInternalAsync(runtime, cancellationToken);
            }
            else if (status.Status == CollectorStatus.Disabled && runtime.Config.Enabled)
            {
                await StartCollectorInternalAsync(runtime, cancellationToken);
            }
            else if (status.Status == CollectorStatus.Running)
            {
                runtime.RetryCount = 0;
                runtime.NextRetryAtUtc = DateTimeOffset.MinValue;
            }
        }
    }

    public async Task<IReadOnlyList<CollectorDetails>> GetCollectorsAsync(CancellationToken cancellationToken)
    {
        var items = new List<CollectorDetails>(_runtimes.Count);
        foreach (var runtime in _runtimes.Values.OrderBy(x => x.Collector.CollectorId))
        {
            runtime.State = await runtime.Collector.GetStatusAsync(cancellationToken);
            items.Add(ToDetails(runtime));
        }

        return items;
    }

    public async Task<CollectorDetails> GetCollectorAsync(string collectorId, CancellationToken cancellationToken)
    {
        var runtime = GetRuntime(collectorId);
        runtime.State = await runtime.Collector.GetStatusAsync(cancellationToken);
        return ToDetails(runtime);
    }

    public async Task StartCollectorAsync(string collectorId, CancellationToken cancellationToken)
    {
        var runtime = GetRuntime(collectorId);
        runtime.Config = runtime.Config with
        {
            Enabled = true,
            Version = runtime.Config.Version + 1,
            UpdatedAtUtc = _clock.UtcNow,
        };
        await _configRepository.UpsertAsync(runtime.Config, cancellationToken);
        await StartCollectorInternalAsync(runtime, cancellationToken);
    }

    public async Task StopCollectorAsync(string collectorId, CancellationToken cancellationToken)
    {
        var runtime = GetRuntime(collectorId);
        runtime.Config = runtime.Config with
        {
            Enabled = false,
            Version = runtime.Config.Version + 1,
            UpdatedAtUtc = _clock.UtcNow,
        };
        await _configRepository.UpsertAsync(runtime.Config, cancellationToken);
        await StopCollectorInternalAsync(runtime, cancellationToken, persistDisabledState: true);
    }

    public async Task<CollectorDetails> UpdateCollectorConfigAsync(string collectorId, bool enabled, string settingsJson, CancellationToken cancellationToken)
    {
        JsonHelpers.ParseObject(settingsJson, "settings");

        var runtime = GetRuntime(collectorId);
        runtime.Config = runtime.Config with
        {
            Enabled = enabled,
            SettingsJson = settingsJson,
            Version = runtime.Config.Version + 1,
            UpdatedAtUtc = _clock.UtcNow,
        };

        await _configRepository.UpsertAsync(runtime.Config, cancellationToken);
        await runtime.Collector.UpdateConfigAsync(runtime.Config, cancellationToken);

        if (enabled)
        {
            await StartCollectorInternalAsync(runtime, cancellationToken);
        }
        else
        {
            await StopCollectorInternalAsync(runtime, cancellationToken, persistDisabledState: true);
        }

        runtime.State = await runtime.Collector.GetStatusAsync(cancellationToken);
        await _stateRepository.UpsertAsync(runtime.State, cancellationToken);
        return ToDetails(runtime);
    }

    private async Task StartCollectorInternalAsync(CollectorRuntime runtime, CancellationToken cancellationToken)
    {
        if (runtime.Capability.SupportLevel == CapabilitySupportLevel.Unsupported)
        {
            runtime.State = new CollectorState(runtime.Collector.CollectorId, CollectorStatus.Unsupported, null, runtime.Capability.Reason, _clock.UtcNow);
            await _stateRepository.UpsertAsync(runtime.State, cancellationToken);
            return;
        }

        await runtime.Collector.StartAsync(runtime.Config, cancellationToken);
        runtime.State = await runtime.Collector.GetStatusAsync(cancellationToken);
        await _stateRepository.UpsertAsync(runtime.State, cancellationToken);
    }

    private async Task StopCollectorInternalAsync(CollectorRuntime runtime, CancellationToken cancellationToken, bool persistDisabledState)
    {
        await runtime.Collector.StopAsync(cancellationToken);
        runtime.State = await runtime.Collector.GetStatusAsync(cancellationToken);
        if (persistDisabledState)
        {
            runtime.State = runtime.State with { Status = CollectorStatus.Disabled, UpdatedAtUtc = _clock.UtcNow };
        }

        await _stateRepository.UpsertAsync(runtime.State, cancellationToken);
    }

    private CollectorRuntime GetRuntime(string collectorId)
    {
        if (_runtimes.TryGetValue(collectorId, out var runtime))
        {
            return runtime;
        }

        throw new ApiException(HttpStatusCode.NotFound, "collector_not_found", $"Collector '{collectorId}' was not found.");
    }

    private static CollectorDetails ToDetails(CollectorRuntime runtime)
    {
        JsonObject? settings = null;
        if (!string.IsNullOrWhiteSpace(runtime.Config.SettingsJson))
        {
            try
            {
                settings = JsonNode.Parse(runtime.Config.SettingsJson) as JsonObject;
            }
            catch
            {
            }
        }

        return new CollectorDetails(
            runtime.Collector.CollectorId,
            runtime.Collector.DisplayName,
            runtime.Config.Enabled,
            runtime.State.Status,
            runtime.Capability.SupportLevel,
            runtime.Capability.Reason,
            runtime.State.LastHeartbeatUtc,
            runtime.State.LastError,
            runtime.Config.Version,
            settings);
    }

    private sealed class CollectorRuntime
    {
        public CollectorRuntime(ICollector collector)
        {
            Collector = collector;
            Config = new CollectorConfig(collector.CollectorId, true, 1, DateTimeOffset.UtcNow, "{}");
            State = new CollectorState(collector.CollectorId, CollectorStatus.Disabled, null, null, DateTimeOffset.UtcNow);
            Capability = new PlatformCapability(collector.CollectorId, CapabilitySupportLevel.Supported, null);
        }

        public ICollector Collector { get; }

        public CollectorConfig Config { get; set; }

        public CollectorState State { get; set; }

        public PlatformCapability Capability { get; set; }

        public int RetryCount { get; set; }

        public DateTimeOffset NextRetryAtUtc { get; set; }
    }
}
