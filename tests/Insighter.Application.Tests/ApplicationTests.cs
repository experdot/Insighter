using Insighter.Application;
using Insighter.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace Insighter.Application.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public void JsonContentValidator_Accepts_ValidEnvelope()
    {
        var validator = new JsonContentValidator();
        var logEvent = new LogEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            EventTypes.SystemStartup,
            CollectorIds.SystemLifecycle,
            EventSeverity.Information,
            "1",
            "machine",
            """{"schemaVersion":1,"platform":"windows","data":{"source":"test"}}""");

        validator.Validate(logEvent);
    }

    [Fact]
    public void JsonContentValidator_Rejects_InvalidEnvelope()
    {
        var validator = new JsonContentValidator();
        var logEvent = new LogEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            EventTypes.SystemStartup,
            CollectorIds.SystemLifecycle,
            EventSeverity.Information,
            "1",
            "machine",
            """{"platform":"windows","data":{}}""");

        Assert.Throws<InvalidOperationException>(() => validator.Validate(logEvent));
    }

    [Fact]
    public async Task RedactionService_Truncates_Window_Title_And_Clipboard_Content()
    {
        var repository = new InMemoryAppSettingsRepository(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SettingsKeys.DefaultWindowTitleMaxLength] = "5",
            [SettingsKeys.DefaultClipboardMaxLength] = "4",
            [SettingsKeys.DefaultClipboardMode] = nameof(ClipboardCaptureMode.Preview),
        });

        var settingsService = new SettingsService(repository, Options.Create(new InsighterOptions()));
        var service = new DefaultRedactionService(settingsService, NullLogger<DefaultRedactionService>.Instance);

        var windowEvent = new LogEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            EventTypes.WindowActivated,
            CollectorIds.ForegroundWindow,
            EventSeverity.Information,
            "1",
            "machine",
            """{"schemaVersion":1,"platform":"windows","data":{"processName":"code","windowTitle":"abcdef"}}""");

        var clipboardEvent = new LogEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            EventTypes.ClipboardChanged,
            CollectorIds.Clipboard,
            EventSeverity.Information,
            "1",
            "machine",
            """{"schemaVersion":1,"platform":"windows","data":{"captureMode":"Preview","preview":"abcdef"}}""");

        var redactedWindow = await service.RedactAsync(windowEvent, CancellationToken.None);
        var redactedClipboard = await service.RedactAsync(clipboardEvent, CancellationToken.None);

        Assert.Contains("abcde", redactedWindow.Content);
        Assert.Contains("abcd", redactedClipboard.Content);
    }

    [Fact]
    public async Task LogAggregationService_Computes_Idle_Duration()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var endedAt = startedAt.AddMinutes(3);
        var repository = new InMemoryLogEventRepository(
        [
            new LogEvent(Guid.NewGuid(), startedAt, EventTypes.UserIdleStarted, CollectorIds.Idle, EventSeverity.Information, "1", "m", """{"schemaVersion":1,"platform":"windows","data":{}}"""),
            new LogEvent(Guid.NewGuid(), endedAt, EventTypes.UserIdleEnded, CollectorIds.Idle, EventSeverity.Information, "1", "m", """{"schemaVersion":1,"platform":"windows","data":{}}"""),
            new LogEvent(Guid.NewGuid(), endedAt.AddMinutes(1), EventTypes.WindowActivated, CollectorIds.ForegroundWindow, EventSeverity.Information, "1", "m", """{"schemaVersion":1,"platform":"windows","data":{}}"""),
        ]);

        var service = new LogAggregationService(repository);
        var summary = await service.GetSummaryAsync(startedAt.AddMinutes(-1), endedAt.AddMinutes(2), CancellationToken.None);

        Assert.Equal(3, summary.TotalEvents);
        Assert.Equal(1, summary.WindowActivationCount);
        Assert.Equal(1, summary.IdlePeriodCount);
        Assert.Equal(180, summary.TotalIdleDurationSeconds);
    }

    [Fact]
    public void CollectorConfigDefaults_Builds_Defaults_For_NewCollectors()
    {
        var settings = new AppSettingsSnapshot(
            "db",
            "http://127.0.0.1:5078",
            4096,
            30,
            500,
            ClipboardCaptureMode.Preview,
            200,
            200,
            [],
            true);

        var processSettings = JsonNode.Parse(CollectorConfigDefaults.BuildSettingsJson(CollectorIds.ProcessLifecycle, settings)) as JsonObject;
        var powerSettings = JsonNode.Parse(CollectorConfigDefaults.BuildSettingsJson(CollectorIds.Power, settings)) as JsonObject;
        var networkSettings = JsonNode.Parse(CollectorConfigDefaults.BuildSettingsJson(CollectorIds.NetworkConnectivity, settings)) as JsonObject;

        Assert.NotNull(processSettings);
        Assert.NotNull(powerSettings);
        Assert.NotNull(networkSettings);

        Assert.Equal(true, processSettings!["captureExecutablePath"]?.GetValue<bool>());
        Assert.Equal(true, processSettings["ignoreCurrentProcess"]?.GetValue<bool>());
        Assert.Equal(5, powerSettings!["batteryLevelStepPercent"]?.GetValue<int>());
        Assert.Equal(15, powerSettings["pollIntervalSeconds"]?.GetValue<int>());
        Assert.Equal(5, networkSettings!["pollIntervalSeconds"]?.GetValue<int>());
        Assert.Equal(true, networkSettings["reportActiveNetworkChanges"]?.GetValue<bool>());
    }

    private sealed class InMemoryAppSettingsRepository : IAppSettingsRepository
    {
        private readonly Dictionary<string, string> _values;

        public InMemoryAppSettingsRepository(Dictionary<string, string> values)
        {
            _values = values;
        }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyDictionary<string, string>)_values);

        public Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task UpsertManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
        {
            foreach (var pair in values)
            {
                _values[pair.Key] = pair.Value;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryLogEventRepository : ILogEventRepository
    {
        private readonly IReadOnlyList<LogEvent> _events;

        public InMemoryLogEventRepository(IReadOnlyList<LogEvent> events)
        {
            _events = events;
        }

        public Task InsertBatchAsync(IReadOnlyCollection<LogEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<PagedResult<LogEvent>> SearchAsync(LogQueryFilter filter, CancellationToken cancellationToken)
            => Task.FromResult(new PagedResult<LogEvent>(_events, 1, _events.Count, _events.Count));

        public Task<LogEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(_events.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<LogEvent>> GetRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyList<LogEvent>)_events.Where(x => x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc <= toUtc).ToArray());
    }
}
