using System.Text.Json.Nodes;
using System.Threading.Channels;
using Insighter.Application;
using Insighter.Domain;
using Insighter.Infrastructure.Platform.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Insighter.IntegrationTests;

public sealed class WindowsCollectorTests
{
    [Fact]
    public async Task SystemLifecycleCollector_Emits_Sleep_And_Resume()
    {
        var sink = new RecordingEventSink();
        var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var monitor = new TestSystemLifecycleMonitor();
        var lifetime = new TestHostApplicationLifetime();
        var collector = new SystemLifecycleCollector(
            sink,
            clock,
            new TestMachineContextProvider(),
            new TestUserSessionContextProvider(),
            lifetime,
            monitor,
            NullLogger<SystemLifecycleCollector>.Instance);

        await collector.StartAsync(new CollectorConfig(CollectorIds.SystemLifecycle, true, 1, clock.UtcNow, "{}"), CancellationToken.None);
        await Task.Delay(100);

        var startup = await sink.ReadAsync();
        Assert.Equal(EventTypes.SystemStartup, startup.EventType);

        monitor.Enqueue(new SystemLifecycleSignal(SystemLifecycleSignalKind.Sleep));
        var sleep = await sink.ReadAsync();
        Assert.Equal(EventTypes.SystemSleep, sleep.EventType);

        monitor.Enqueue(new SystemLifecycleSignal(SystemLifecycleSignalKind.Resume));
        var resumed = await sink.ReadAsync();
        Assert.Equal(EventTypes.SystemResumed, resumed.EventType);

        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ForegroundWindowCollector_Redacts_Blacklisted_Process_And_Trims_Allowed_Titles()
    {
        var sink = new RecordingEventSink();
        var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var monitor = new TestForegroundWindowMonitor(new ForegroundWindowSnapshot((nint)1, 42, "Code.exe", "Secret document"));
        var collector = new ForegroundWindowCollector(
            sink,
            clock,
            new TestMachineContextProvider(),
            new TestUserSessionContextProvider(),
            monitor,
            NullLogger<ForegroundWindowCollector>.Instance);

        await collector.StartAsync(
            new CollectorConfig(
                CollectorIds.ForegroundWindow,
                true,
                1,
                clock.UtcNow,
                JsonHelpers.Serialize(new
                {
                    titleMaxLength = 5,
                    processBlacklist = new[] { "code" },
                })),
            CancellationToken.None);

        var redacted = await sink.ReadAsync();
        var redactedData = GetData(redacted);
        Assert.Equal(EventTypes.WindowActivated, redacted.EventType);
        Assert.Equal("[REDACTED]", redactedData["windowTitle"]?.GetValue<string>());
        Assert.Equal(true, redactedData["redacted"]?.GetValue<bool>());
        Assert.Null(redactedData["windowTitleHash"]);

        monitor.SetCurrent(new ForegroundWindowSnapshot((nint)2, 84, "notepad.exe", "abcdef"));
        var trimmed = await sink.ReadAsync();
        var trimmedData = GetData(trimmed);
        Assert.Equal("abcde", trimmedData["windowTitle"]?.GetValue<string>());
        Assert.Null(trimmedData["redacted"]);
        Assert.NotNull(trimmedData["windowTitleHash"]?.GetValue<string>());

        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ClipboardCollector_Respects_TextOnly_And_CaptureMode()
    {
        var sink = new RecordingEventSink();
        var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var monitor = new TestClipboardMonitor(new ClipboardSnapshot(1, false, null));
        var collector = new ClipboardCollector(
            sink,
            clock,
            new TestMachineContextProvider(),
            new TestUserSessionContextProvider(),
            monitor,
            NullLogger<ClipboardCollector>.Instance);

        await collector.StartAsync(
            new CollectorConfig(
                CollectorIds.Clipboard,
                true,
                1,
                clock.UtcNow,
                JsonHelpers.Serialize(new
                {
                    captureMode = nameof(ClipboardCaptureMode.Preview),
                    maxContentLength = 4,
                    textOnly = true,
                })),
            CancellationToken.None);

        await Task.Delay(800);
        Assert.False(sink.TryRead(out _));

        monitor.SetCurrent(new ClipboardSnapshot(2, true, "abcdef"));
        var previewEvent = await sink.ReadAsync();
        var previewData = GetData(previewEvent);
        Assert.Equal(EventTypes.ClipboardChanged, previewEvent.EventType);
        Assert.Equal(nameof(ClipboardCaptureMode.Preview), previewData["captureMode"]?.GetValue<string>());
        Assert.Equal("abcd", previewData["preview"]?.GetValue<string>());
        Assert.Null(previewData["text"]);
        Assert.NotNull(previewData["textHash"]?.GetValue<string>());

        await collector.UpdateConfigAsync(
            new CollectorConfig(
                CollectorIds.Clipboard,
                true,
                2,
                clock.UtcNow,
                JsonHelpers.Serialize(new
                {
                    captureMode = nameof(ClipboardCaptureMode.FullText),
                    maxContentLength = 3,
                    textOnly = false,
                })),
            CancellationToken.None);

        monitor.SetCurrent(new ClipboardSnapshot(3, false, null));
        var nonTextEvent = await sink.ReadAsync();
        var nonTextData = GetData(nonTextEvent);
        Assert.Equal(false, nonTextData["hasText"]?.GetValue<bool>());
        Assert.Equal(nameof(ClipboardCaptureMode.FullText), nonTextData["captureMode"]?.GetValue<string>());
        Assert.Null(nonTextData["preview"]);
        Assert.Null(nonTextData["text"]);
        Assert.Null(nonTextData["textHash"]);

        monitor.SetCurrent(new ClipboardSnapshot(4, true, "xyz123"));
        var fullTextEvent = await sink.ReadAsync();
        var fullTextData = GetData(fullTextEvent);
        Assert.Equal("xyz", fullTextData["text"]?.GetValue<string>());
        Assert.Null(fullTextData["preview"]);

        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PowerCollector_Only_Emits_When_Battery_Step_Is_Crossed()
    {
        var sink = new RecordingEventSink();
        var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var monitor = new TestPowerStatusMonitor(new PowerStatusSnapshot(
            PowerSourceKind.Battery,
            PowerChargingState.Discharging,
            84));
        var collector = new PowerCollector(
            sink,
            clock,
            new TestMachineContextProvider(),
            new TestUserSessionContextProvider(),
            monitor,
            NullLogger<PowerCollector>.Instance);

        await collector.StartAsync(
            new CollectorConfig(
                CollectorIds.Power,
                true,
                1,
                clock.UtcNow,
                JsonHelpers.Serialize(new
                {
                    batteryLevelStepPercent = 5,
                    reportPowerSourceChanges = true,
                    reportChargingStateChanges = true,
                    reportBatteryLevelChanges = true,
                    pollIntervalSeconds = 60,
                })),
            CancellationToken.None);
        await Task.Delay(100);

        monitor.Signal(new PowerStatusSnapshot(PowerSourceKind.Battery, PowerChargingState.Discharging, 82));
        await Task.Delay(150);
        Assert.False(sink.TryRead(out _));

        monitor.Signal(new PowerStatusSnapshot(PowerSourceKind.Battery, PowerChargingState.Discharging, 79));
        var batteryLevelChanged = await sink.ReadAsync();
        var powerData = GetData(batteryLevelChanged);
        Assert.Equal(EventTypes.PowerBatteryLevelChanged, batteryLevelChanged.EventType);
        Assert.Equal(79, powerData["currentBatteryPercent"]?.GetValue<int>());

        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NetworkConnectivityCollector_Emits_Online_Transition()
    {
        var sink = new RecordingEventSink();
        var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var monitor = new TestNetworkConnectivityMonitor(new NetworkConnectivitySnapshot(
            false,
            null,
            null,
            null));
        var collector = new NetworkConnectivityCollector(
            sink,
            clock,
            new TestMachineContextProvider(),
            new TestUserSessionContextProvider(),
            monitor,
            NullLogger<NetworkConnectivityCollector>.Instance);

        await collector.StartAsync(
            new CollectorConfig(
                CollectorIds.NetworkConnectivity,
                true,
                1,
                clock.UtcNow,
                JsonHelpers.Serialize(new
                {
                    pollIntervalSeconds = 60,
                    reportActiveNetworkChanges = true,
                })),
            CancellationToken.None);
        await Task.Delay(100);

        monitor.Signal(new NetworkConnectivitySnapshot(true, "eth0", "Ethernet", "Ethernet"));
        var online = await sink.ReadAsync();
        Assert.Equal(EventTypes.NetworkOnline, online.EventType);

        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NetworkConnectivityCollector_Emits_Active_Network_Changes()
    {
        var sink = new RecordingEventSink();
        var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var monitor = new TestNetworkConnectivityMonitor(new NetworkConnectivitySnapshot(
            true,
            "eth0",
            "Ethernet",
            "Ethernet"));
        var collector = new NetworkConnectivityCollector(
            sink,
            clock,
            new TestMachineContextProvider(),
            new TestUserSessionContextProvider(),
            monitor,
            NullLogger<NetworkConnectivityCollector>.Instance);

        await collector.StartAsync(
            new CollectorConfig(
                CollectorIds.NetworkConnectivity,
                true,
                1,
                clock.UtcNow,
                JsonHelpers.Serialize(new
                {
                    pollIntervalSeconds = 60,
                    reportActiveNetworkChanges = true,
                })),
            CancellationToken.None);
        await Task.Delay(100);

        monitor.Signal(new NetworkConnectivitySnapshot(true, "wifi0", "Wi-Fi", "Wireless80211"));
        var changed = await sink.ReadAsync();
        var networkData = GetData(changed);
        Assert.Equal(EventTypes.NetworkActiveNetworkChanged, changed.EventType);
        Assert.Equal("Wi-Fi", networkData["currentInterfaceName"]?.GetValue<string>());

        await collector.StopAsync(CancellationToken.None);
    }

    private sealed class RecordingEventSink : IEventSink
    {
        private readonly Channel<LogEvent> _events = Channel.CreateUnbounded<LogEvent>();

        public ValueTask WriteAsync(LogEvent logEvent, CancellationToken cancellationToken)
        {
            _events.Writer.TryWrite(logEvent);
            return ValueTask.CompletedTask;
        }

        public async Task<LogEvent> ReadAsync(CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            return await _events.Reader.ReadAsync(cts.Token);
        }

        public bool TryRead(out LogEvent logEvent)
        {
            if (_events.Reader.TryRead(out var candidate))
            {
                logEvent = candidate;
                return true;
            }

            logEvent = new LogEvent(
                Guid.Empty,
                DateTimeOffset.UnixEpoch,
                string.Empty,
                string.Empty,
                EventSeverity.Information,
                string.Empty,
                string.Empty,
                "{}");
            return false;
        }
    }

    private sealed class ManualClock : IClock
    {
        public ManualClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan delta)
        {
            UtcNow = UtcNow.Add(delta);
        }
    }

    private sealed class TestMachineContextProvider : IMachineContextProvider
    {
        public string GetMachineId() => "machine";

        public string GetPlatform() => "windows";
    }

    private sealed class TestUserSessionContextProvider : IUserSessionContextProvider
    {
        public string GetSessionId() => "session";
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        public void StopApplication()
        {
            _stopping.Cancel();
            _stopped.Cancel();
        }
    }

    private sealed class TestSystemLifecycleMonitor : ISystemLifecycleMonitor
    {
        private readonly Channel<SystemLifecycleSignal> _signals = Channel.CreateUnbounded<SystemLifecycleSignal>();

        public void Enqueue(SystemLifecycleSignal signal)
        {
            _signals.Writer.TryWrite(signal);
        }

        public ValueTask<SystemLifecycleSignal> WaitForNextAsync(CancellationToken cancellationToken)
            => _signals.Reader.ReadAsync(cancellationToken);
    }

    private sealed class TestForegroundWindowMonitor : IForegroundWindowMonitor
    {
        private ForegroundWindowSnapshot? _current;

        public TestForegroundWindowMonitor(ForegroundWindowSnapshot? current)
        {
            _current = current;
        }

        public void SetCurrent(ForegroundWindowSnapshot? current)
        {
            _current = current;
        }

        public ValueTask<ForegroundWindowSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(_current);
    }

    private sealed class TestClipboardMonitor : IClipboardMonitor
    {
        private ClipboardSnapshot? _current;

        public TestClipboardMonitor(ClipboardSnapshot? current)
        {
            _current = current;
        }

        public void SetCurrent(ClipboardSnapshot? current)
        {
            _current = current;
        }

        public ValueTask<ClipboardSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(_current);
    }

    private sealed class TestPowerStatusMonitor : IPowerStatusMonitor
    {
        private readonly Channel<bool> _signals = Channel.CreateUnbounded<bool>();
        private PowerStatusSnapshot _current;

        public TestPowerStatusMonitor(PowerStatusSnapshot current)
        {
            _current = current;
        }

        public ValueTask<PowerStatusSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(_current);

        public void Signal(PowerStatusSnapshot snapshot)
        {
            _current = snapshot;
            _signals.Writer.TryWrite(true);
        }

        public async ValueTask WaitForChangeAsync(CancellationToken cancellationToken)
        {
            await _signals.Reader.ReadAsync(cancellationToken);
        }
    }

    private sealed class TestNetworkConnectivityMonitor : INetworkConnectivityMonitor
    {
        private readonly Channel<bool> _signals = Channel.CreateUnbounded<bool>();
        private NetworkConnectivitySnapshot _current;

        public TestNetworkConnectivityMonitor(NetworkConnectivitySnapshot current)
        {
            _current = current;
        }

        public ValueTask<NetworkConnectivitySnapshot> GetCurrentAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(_current);

        public void Signal(NetworkConnectivitySnapshot snapshot)
        {
            _current = snapshot;
            _signals.Writer.TryWrite(true);
        }

        public async ValueTask WaitForChangeAsync(CancellationToken cancellationToken)
        {
            await _signals.Reader.ReadAsync(cancellationToken);
        }
    }

    private static JsonObject GetData(LogEvent logEvent)
        => JsonNode.Parse(logEvent.Content)?["data"] as JsonObject ?? new JsonObject();
}
