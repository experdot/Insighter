using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Insighter.Application;
using Insighter.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Insighter.Infrastructure.Platform.Windows;

public sealed class SystemLifecycleCollector : BaseCollector
{
    private readonly IEventSink _eventSink;
    private readonly IClock _clock;
    private readonly IMachineContextProvider _machineContextProvider;
    private readonly IUserSessionContextProvider _userSessionContextProvider;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ISystemLifecycleMonitor _monitor;
    private int _startupEmitted;

    public SystemLifecycleCollector(
        IEventSink eventSink,
        IClock clock,
        IMachineContextProvider machineContextProvider,
        IUserSessionContextProvider userSessionContextProvider,
        IHostApplicationLifetime applicationLifetime,
        ISystemLifecycleMonitor monitor,
        ILogger<SystemLifecycleCollector> logger)
        : base(CollectorIds.SystemLifecycle, "System Lifecycle", clock, logger)
    {
        _eventSink = eventSink;
        _clock = clock;
        _machineContextProvider = machineContextProvider;
        _userSessionContextProvider = userSessionContextProvider;
        _applicationLifetime = applicationLifetime;
        _monitor = monitor;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _startupEmitted, 1) == 0)
        {
            await PublishAsync(EventTypes.SystemStartup, new JsonObject { ["source"] = "host-startup" }, cancellationToken);
        }

        using var registration = _applicationLifetime.ApplicationStopping.Register(() =>
        {
            PublishAsync(EventTypes.SystemShutdown, new JsonObject { ["source"] = "host-stopping" }, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        });

        while (!cancellationToken.IsCancellationRequested)
        {
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var signalTask = _monitor.WaitForNextAsync(waitCts.Token).AsTask();
            var delayTask = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            var completed = await Task.WhenAny(signalTask, delayTask);

            if (completed == signalTask)
            {
                var signal = await signalTask;
                var eventType = signal.Kind == SystemLifecycleSignalKind.Sleep
                    ? EventTypes.SystemSleep
                    : EventTypes.SystemResumed;

                await PublishAsync(
                    eventType,
                    new JsonObject
                    {
                        ["source"] = "system-power",
                        ["signal"] = signal.Kind.ToString(),
                    },
                    cancellationToken);
            }
            else
            {
                waitCts.Cancel();
                try
                {
                    await signalTask;
                }
                catch (OperationCanceledException) when (waitCts.IsCancellationRequested)
                {
                }
            }

            TouchHeartbeat();
        }
    }

    protected override Task OnConfigUpdatedAsync(CollectorConfig config, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private ValueTask PublishAsync(string eventType, JsonObject data, CancellationToken cancellationToken)
        => _eventSink.WriteAsync(CreateEvent(eventType, data), cancellationToken);

    private LogEvent CreateEvent(string eventType, JsonObject data)
        => new(
            Guid.NewGuid(),
            _clock.UtcNow,
            eventType,
            CollectorId,
            EventSeverity.Information,
            _userSessionContextProvider.GetSessionId(),
            _machineContextProvider.GetMachineId(),
            BuildContent(_machineContextProvider.GetPlatform(), data));

    private static string BuildContent(string platform, JsonObject data)
        => new JsonObject
        {
            ["schemaVersion"] = 1,
            ["platform"] = platform,
            ["data"] = data,
        }.ToJsonString(JsonHelpers.SerializerOptions);
}

public sealed class SessionStateCollector : BaseCollector
{
    private readonly IEventSink _eventSink;
    private readonly IClock _clock;
    private readonly IMachineContextProvider _machineContextProvider;
    private readonly IUserSessionContextProvider _userSessionContextProvider;
    private readonly ISessionStateMonitor _monitor;
    private SessionLockState _lastState = SessionLockState.Unknown;

    public SessionStateCollector(
        IEventSink eventSink,
        IClock clock,
        IMachineContextProvider machineContextProvider,
        IUserSessionContextProvider userSessionContextProvider,
        ISessionStateMonitor monitor,
        ILogger<SessionStateCollector> logger)
        : base(CollectorIds.SessionState, "Session State", clock, logger)
    {
        _eventSink = eventSink;
        _clock = clock;
        _machineContextProvider = machineContextProvider;
        _userSessionContextProvider = userSessionContextProvider;
        _monitor = monitor;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var current = await _monitor.GetCurrentAsync(cancellationToken);
            if (current != SessionLockState.Unknown && current != _lastState)
            {
                _lastState = current;
                var eventType = current == SessionLockState.Locked ? EventTypes.SessionLocked : EventTypes.SessionUnlocked;
                await _eventSink.WriteAsync(CreateEvent(eventType, new JsonObject { ["state"] = current.ToString() }), cancellationToken);
            }

            TouchHeartbeat();
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    protected override Task OnConfigUpdatedAsync(CollectorConfig config, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private LogEvent CreateEvent(string eventType, JsonObject data)
        => new(
            Guid.NewGuid(),
            _clock.UtcNow,
            eventType,
            CollectorId,
            EventSeverity.Information,
            _userSessionContextProvider.GetSessionId(),
            _machineContextProvider.GetMachineId(),
            BuildContent(_machineContextProvider.GetPlatform(), data));

    private static string BuildContent(string platform, JsonObject data)
        => new JsonObject { ["schemaVersion"] = 1, ["platform"] = platform, ["data"] = data }.ToJsonString(JsonHelpers.SerializerOptions);
}

public sealed class ForegroundWindowCollector : BaseCollector
{
    private const string RedactedWindowTitle = "[REDACTED]";

    private readonly IEventSink _eventSink;
    private readonly IClock _clock;
    private readonly IMachineContextProvider _machineContextProvider;
    private readonly IUserSessionContextProvider _userSessionContextProvider;
    private readonly IForegroundWindowMonitor _monitor;
    private ForegroundWindowCollectorSettings _settings = new();
    private HashSet<string> _processBlacklist = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastFingerprint;

    public ForegroundWindowCollector(
        IEventSink eventSink,
        IClock clock,
        IMachineContextProvider machineContextProvider,
        IUserSessionContextProvider userSessionContextProvider,
        IForegroundWindowMonitor monitor,
        ILogger<ForegroundWindowCollector> logger)
        : base(CollectorIds.ForegroundWindow, "Foreground Window", clock, logger)
    {
        _eventSink = eventSink;
        _clock = clock;
        _machineContextProvider = machineContextProvider;
        _userSessionContextProvider = userSessionContextProvider;
        _monitor = monitor;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = await _monitor.GetCurrentAsync(cancellationToken);
            if (snapshot is not null)
            {
                var title = snapshot.WindowTitle ?? string.Empty;
                var processName = CollectorHelpers.NormalizeProcessName(snapshot.ProcessName);
                if (string.IsNullOrWhiteSpace(processName))
                {
                    processName = snapshot.ProcessName;
                }

                var fingerprint = $"{snapshot.ProcessId}:{title}";
                if (!string.Equals(_lastFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    _lastFingerprint = fingerprint;
                    var redacted = _processBlacklist.Contains(processName);
                    var data = new JsonObject
                    {
                        ["processId"] = snapshot.ProcessId,
                        ["processName"] = processName,
                        ["windowTitle"] = redacted ? RedactedWindowTitle : Trim(title, _settings.TitleMaxLength),
                    };

                    if (redacted)
                    {
                        data["redacted"] = true;
                    }
                    else
                    {
                        data["windowTitleHash"] = ComputeSha256(title);
                    }

                    await _eventSink.WriteAsync(CreateEvent(EventTypes.WindowActivated, data), cancellationToken);
                }
            }

            TouchHeartbeat();
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }

    protected override Task OnConfigUpdatedAsync(CollectorConfig config, CancellationToken cancellationToken)
    {
        _settings = JsonHelpers.Deserialize<ForegroundWindowCollectorSettings>(config.SettingsJson) ?? new ForegroundWindowCollectorSettings();
        _processBlacklist = _settings.ProcessBlacklist
            .Select(CollectorHelpers.NormalizeProcessName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.CompletedTask;
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
            BuildContent(_machineContextProvider.GetPlatform(), data));

    private static string BuildContent(string platform, JsonObject data)
        => new JsonObject { ["schemaVersion"] = 1, ["platform"] = platform, ["data"] = data }.ToJsonString(JsonHelpers.SerializerOptions);

    private static string Trim(string value, int maxLength)
        => maxLength > 0 && value.Length > maxLength ? value[..maxLength] : value;

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}

public sealed class IdleCollector : BaseCollector
{
    private readonly IEventSink _eventSink;
    private readonly IClock _clock;
    private readonly IMachineContextProvider _machineContextProvider;
    private readonly IUserSessionContextProvider _userSessionContextProvider;
    private readonly IIdleMonitor _idleMonitor;
    private readonly ISessionStateMonitor _sessionStateMonitor;
    private IdleCollectorSettings _settings = new();
    private bool _idle;

    public IdleCollector(
        IEventSink eventSink,
        IClock clock,
        IMachineContextProvider machineContextProvider,
        IUserSessionContextProvider userSessionContextProvider,
        IIdleMonitor idleMonitor,
        ISessionStateMonitor sessionStateMonitor,
        ILogger<IdleCollector> logger)
        : base(CollectorIds.Idle, "Idle State", clock, logger)
    {
        _eventSink = eventSink;
        _clock = clock;
        _machineContextProvider = machineContextProvider;
        _userSessionContextProvider = userSessionContextProvider;
        _idleMonitor = idleMonitor;
        _sessionStateMonitor = sessionStateMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_settings.IgnoreWhileLocked)
            {
                var session = await _sessionStateMonitor.GetCurrentAsync(cancellationToken);
                if (session == SessionLockState.Locked)
                {
                    TouchHeartbeat();
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }
            }

            var idleTime = await _idleMonitor.GetIdleTimeAsync(cancellationToken);
            var threshold = TimeSpan.FromSeconds(Math.Max(1, _settings.IdleThresholdSeconds));

            if (!_idle && idleTime >= threshold)
            {
                _idle = true;
                await _eventSink.WriteAsync(
                    CreateEvent(EventTypes.UserIdleStarted, new JsonObject
                    {
                        ["idleThresholdSeconds"] = _settings.IdleThresholdSeconds,
                        ["idleSeconds"] = Math.Round(idleTime.TotalSeconds, 0),
                    }),
                    cancellationToken);
            }
            else if (_idle && idleTime < threshold)
            {
                _idle = false;
                await _eventSink.WriteAsync(
                    CreateEvent(EventTypes.UserIdleEnded, new JsonObject
                    {
                        ["idleThresholdSeconds"] = _settings.IdleThresholdSeconds,
                        ["idleSeconds"] = Math.Round(idleTime.TotalSeconds, 0),
                    }),
                    cancellationToken);
            }

            TouchHeartbeat();
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    protected override Task OnConfigUpdatedAsync(CollectorConfig config, CancellationToken cancellationToken)
    {
        _settings = JsonHelpers.Deserialize<IdleCollectorSettings>(config.SettingsJson) ?? new IdleCollectorSettings();
        return Task.CompletedTask;
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
            BuildContent(_machineContextProvider.GetPlatform(), data));

    private static string BuildContent(string platform, JsonObject data)
        => new JsonObject { ["schemaVersion"] = 1, ["platform"] = platform, ["data"] = data }.ToJsonString(JsonHelpers.SerializerOptions);
}

public sealed class ClipboardCollector : BaseCollector
{
    private readonly IEventSink _eventSink;
    private readonly IClock _clock;
    private readonly IMachineContextProvider _machineContextProvider;
    private readonly IUserSessionContextProvider _userSessionContextProvider;
    private readonly IClipboardMonitor _monitor;
    private ClipboardCollectorSettings _settings = new();
    private uint _lastSequence;

    public ClipboardCollector(
        IEventSink eventSink,
        IClock clock,
        IMachineContextProvider machineContextProvider,
        IUserSessionContextProvider userSessionContextProvider,
        IClipboardMonitor monitor,
        ILogger<ClipboardCollector> logger)
        : base(CollectorIds.Clipboard, "Clipboard", clock, logger)
    {
        _eventSink = eventSink;
        _clock = clock;
        _machineContextProvider = machineContextProvider;
        _userSessionContextProvider = userSessionContextProvider;
        _monitor = monitor;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = await _monitor.GetCurrentAsync(cancellationToken);
            if (snapshot is { } current && current.SequenceNumber != _lastSequence)
            {
                _lastSequence = current.SequenceNumber;

                if (TryGetMode(_settings.CaptureMode, out var mode) && mode != ClipboardCaptureMode.Off)
                {
                    if (_settings.TextOnly && !current.HasText)
                    {
                        TouchHeartbeat();
                        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                        continue;
                    }

                    var data = BuildClipboardData(current, mode);
                    await _eventSink.WriteAsync(CreateEvent(EventTypes.ClipboardChanged, data), cancellationToken);
                }
            }

            TouchHeartbeat();
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }

    protected override Task OnConfigUpdatedAsync(CollectorConfig config, CancellationToken cancellationToken)
    {
        _settings = JsonHelpers.Deserialize<ClipboardCollectorSettings>(config.SettingsJson) ?? new ClipboardCollectorSettings();
        return Task.CompletedTask;
    }

    private JsonObject BuildClipboardData(ClipboardSnapshot snapshot, ClipboardCaptureMode mode)
    {
        var data = new JsonObject
        {
            ["sequenceNumber"] = snapshot.SequenceNumber,
            ["hasText"] = snapshot.HasText,
            ["captureMode"] = mode.ToString(),
        };

        if (!snapshot.HasText)
        {
            return data;
        }

        var text = snapshot.Text ?? string.Empty;
        data["textLength"] = text.Length;
        data["textHash"] = ComputeSha256(text);

        if (mode == ClipboardCaptureMode.Preview)
        {
            data["preview"] = Trim(text, _settings.MaxContentLength);
        }
        else if (mode == ClipboardCaptureMode.FullText)
        {
            data["text"] = Trim(text, _settings.MaxContentLength);
        }

        return data;
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
            BuildContent(_machineContextProvider.GetPlatform(), data));

    private static string BuildContent(string platform, JsonObject data)
        => new JsonObject { ["schemaVersion"] = 1, ["platform"] = platform, ["data"] = data }.ToJsonString(JsonHelpers.SerializerOptions);

    private static bool TryGetMode(string value, out ClipboardCaptureMode mode)
        => Enum.TryParse(value, true, out mode);

    private static string Trim(string value, int maxLength)
        => maxLength > 0 && value.Length > maxLength ? value[..maxLength] : value;

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
