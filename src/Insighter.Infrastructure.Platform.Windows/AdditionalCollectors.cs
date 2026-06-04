using System.Text.Json.Nodes;
using Insighter.Application;
using Insighter.Domain;
using Microsoft.Extensions.Logging;

namespace Insighter.Infrastructure.Platform.Windows;

public sealed class PowerCollector : BaseCollector
{
    private readonly IEventSink _eventSink;
    private readonly IClock _clock;
    private readonly IMachineContextProvider _machineContextProvider;
    private readonly IUserSessionContextProvider _userSessionContextProvider;
    private readonly IPowerStatusMonitor _monitor;
    private PowerCollectorSettings _settings = new();
    private PowerStatusSnapshot? _lastSnapshot;

    public PowerCollector(
        IEventSink eventSink,
        IClock clock,
        IMachineContextProvider machineContextProvider,
        IUserSessionContextProvider userSessionContextProvider,
        IPowerStatusMonitor monitor,
        ILogger<PowerCollector> logger)
        : base(CollectorIds.Power, "Power", clock, logger)
    {
        _eventSink = eventSink;
        _clock = clock;
        _machineContextProvider = machineContextProvider;
        _userSessionContextProvider = userSessionContextProvider;
        _monitor = monitor;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _lastSnapshot = await _monitor.GetCurrentAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            await CollectorWaitHelpers.WaitForSignalOrTimeoutAsync(
                token => _monitor.WaitForChangeAsync(token),
                TimeSpan.FromSeconds(Math.Max(1, _settings.PollIntervalSeconds)),
                cancellationToken);

            var current = await _monitor.GetCurrentAsync(cancellationToken);
            if (_lastSnapshot is not null)
            {
                await PublishChangesAsync(_lastSnapshot, current, cancellationToken);
            }

            _lastSnapshot = current;
            TouchHeartbeat();
        }
    }

    protected override Task OnConfigUpdatedAsync(CollectorConfig config, CancellationToken cancellationToken)
    {
        _settings = JsonHelpers.Deserialize<PowerCollectorSettings>(config.SettingsJson) ?? new PowerCollectorSettings();
        return Task.CompletedTask;
    }

    private async Task PublishChangesAsync(PowerStatusSnapshot previous, PowerStatusSnapshot current, CancellationToken cancellationToken)
    {
        if (_settings.ReportPowerSourceChanges && previous.PowerSource != current.PowerSource)
        {
            var data = new JsonObject
            {
                ["previousPowerSource"] = previous.PowerSource.ToString(),
                ["currentPowerSource"] = current.PowerSource.ToString(),
                ["chargingState"] = current.ChargingState.ToString(),
            };

            if (current.BatteryPercent.HasValue)
            {
                data["batteryPercent"] = current.BatteryPercent.Value;
            }

            await _eventSink.WriteAsync(
                CreateEvent(EventTypes.PowerSourceChanged, data),
                cancellationToken);
        }

        if (_settings.ReportChargingStateChanges && previous.ChargingState != current.ChargingState)
        {
            var data = new JsonObject
            {
                ["previousChargingState"] = previous.ChargingState.ToString(),
                ["currentChargingState"] = current.ChargingState.ToString(),
                ["powerSource"] = current.PowerSource.ToString(),
            };

            if (current.BatteryPercent.HasValue)
            {
                data["batteryPercent"] = current.BatteryPercent.Value;
            }

            await _eventSink.WriteAsync(
                CreateEvent(EventTypes.PowerChargingStateChanged, data),
                cancellationToken);
        }

        if (_settings.ReportBatteryLevelChanges && CrossedBatteryStep(previous, current))
        {
            var data = new JsonObject
            {
                ["stepPercent"] = Math.Max(1, _settings.BatteryLevelStepPercent),
                ["chargingState"] = current.ChargingState.ToString(),
                ["powerSource"] = current.PowerSource.ToString(),
            };

            if (previous.BatteryPercent.HasValue)
            {
                data["previousBatteryPercent"] = previous.BatteryPercent.Value;
            }

            if (current.BatteryPercent.HasValue)
            {
                data["currentBatteryPercent"] = current.BatteryPercent.Value;
            }

            await _eventSink.WriteAsync(
                CreateEvent(EventTypes.PowerBatteryLevelChanged, data),
                cancellationToken);
        }
    }

    private bool CrossedBatteryStep(PowerStatusSnapshot previous, PowerStatusSnapshot current)
    {
        if (!previous.BatteryPercent.HasValue || !current.BatteryPercent.HasValue)
        {
            return false;
        }

        var step = Math.Max(1, _settings.BatteryLevelStepPercent);
        return previous.BatteryPercent.Value / step != current.BatteryPercent.Value / step;
    }

    private LogEvent CreateEvent(string eventType, JsonObject data)
        => new(
            Guid.NewGuid(),
            _clock.UtcNow,
            eventType,
            CollectorId,
            EventSeverity.Information,
            _userSessionContextProvider.GetSessionId(),
            _machineContextProvider.GetMachineId(),
            CollectorHelpers.BuildContent(_machineContextProvider.GetPlatform(), data));
}

public sealed class NetworkConnectivityCollector : BaseCollector
{
    private readonly IEventSink _eventSink;
    private readonly IClock _clock;
    private readonly IMachineContextProvider _machineContextProvider;
    private readonly IUserSessionContextProvider _userSessionContextProvider;
    private readonly INetworkConnectivityMonitor _monitor;
    private NetworkConnectivityCollectorSettings _settings = new();
    private NetworkConnectivitySnapshot? _lastSnapshot;

    public NetworkConnectivityCollector(
        IEventSink eventSink,
        IClock clock,
        IMachineContextProvider machineContextProvider,
        IUserSessionContextProvider userSessionContextProvider,
        INetworkConnectivityMonitor monitor,
        ILogger<NetworkConnectivityCollector> logger)
        : base(CollectorIds.NetworkConnectivity, "Network Connectivity", clock, logger)
    {
        _eventSink = eventSink;
        _clock = clock;
        _machineContextProvider = machineContextProvider;
        _userSessionContextProvider = userSessionContextProvider;
        _monitor = monitor;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _lastSnapshot = await _monitor.GetCurrentAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            await CollectorWaitHelpers.WaitForSignalOrTimeoutAsync(
                token => _monitor.WaitForChangeAsync(token),
                TimeSpan.FromSeconds(Math.Max(1, _settings.PollIntervalSeconds)),
                cancellationToken);

            var current = await _monitor.GetCurrentAsync(cancellationToken);
            if (_lastSnapshot is not null)
            {
                await PublishChangesAsync(_lastSnapshot, current, cancellationToken);
            }

            _lastSnapshot = current;
            TouchHeartbeat();
        }
    }

    protected override Task OnConfigUpdatedAsync(CollectorConfig config, CancellationToken cancellationToken)
    {
        _settings = JsonHelpers.Deserialize<NetworkConnectivityCollectorSettings>(config.SettingsJson) ?? new NetworkConnectivityCollectorSettings();
        return Task.CompletedTask;
    }

    private async Task PublishChangesAsync(NetworkConnectivitySnapshot previous, NetworkConnectivitySnapshot current, CancellationToken cancellationToken)
    {
        if (previous.IsOnline != current.IsOnline)
        {
            var data = new JsonObject
            {
                ["isOnline"] = current.IsOnline,
            };

            if (!string.IsNullOrWhiteSpace(current.InterfaceName))
            {
                data["interfaceName"] = current.InterfaceName;
            }

            if (!string.IsNullOrWhiteSpace(current.InterfaceType))
            {
                data["interfaceType"] = current.InterfaceType;
            }

            await _eventSink.WriteAsync(
                CreateEvent(current.IsOnline ? EventTypes.NetworkOnline : EventTypes.NetworkOffline, data),
                cancellationToken);
            return;
        }

        if (_settings.ReportActiveNetworkChanges &&
            current.IsOnline &&
            !string.Equals(previous.InterfaceId, current.InterfaceId, StringComparison.Ordinal))
        {
            var data = new JsonObject();

            if (!string.IsNullOrWhiteSpace(previous.InterfaceName))
            {
                data["previousInterfaceName"] = previous.InterfaceName;
            }

            if (!string.IsNullOrWhiteSpace(previous.InterfaceType))
            {
                data["previousInterfaceType"] = previous.InterfaceType;
            }

            if (!string.IsNullOrWhiteSpace(current.InterfaceName))
            {
                data["currentInterfaceName"] = current.InterfaceName;
            }

            if (!string.IsNullOrWhiteSpace(current.InterfaceType))
            {
                data["currentInterfaceType"] = current.InterfaceType;
            }

            await _eventSink.WriteAsync(
                CreateEvent(EventTypes.NetworkActiveNetworkChanged, data),
                cancellationToken);
        }
    }

    private LogEvent CreateEvent(string eventType, JsonObject data)
        => new(
            Guid.NewGuid(),
            _clock.UtcNow,
            eventType,
            CollectorId,
            EventSeverity.Information,
            _userSessionContextProvider.GetSessionId(),
            _machineContextProvider.GetMachineId(),
            CollectorHelpers.BuildContent(_machineContextProvider.GetPlatform(), data));
}

internal static class CollectorWaitHelpers
{
    public static async Task<(bool HasValue, T Value)> WaitForValueOrTimeoutAsync<T>(
        Func<CancellationToken, ValueTask<T>> readAsync,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var readTask = readAsync(waitCts.Token).AsTask();
        var delayTask = Task.Delay(timeout, cancellationToken);
        var completed = await Task.WhenAny(readTask, delayTask);

        if (completed == readTask)
        {
            return (true, await readTask);
        }

        waitCts.Cancel();
        try
        {
            await readTask;
        }
        catch (OperationCanceledException) when (waitCts.IsCancellationRequested)
        {
        }

        return (false, default!);
    }

    public static async Task WaitForSignalOrTimeoutAsync(
        Func<CancellationToken, ValueTask> waitAsync,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var signalTask = waitAsync(waitCts.Token).AsTask();
        var delayTask = Task.Delay(timeout, cancellationToken);
        var completed = await Task.WhenAny(signalTask, delayTask);

        if (completed == signalTask)
        {
            await signalTask;
            return;
        }

        waitCts.Cancel();
        try
        {
            await signalTask;
        }
        catch (OperationCanceledException) when (waitCts.IsCancellationRequested)
        {
        }
    }
}
