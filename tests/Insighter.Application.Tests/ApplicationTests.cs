using Insighter.Application;
using Insighter.Domain;
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
    public async Task DefaultRedactionService_Returns_Event_Unchanged()
    {
        var service = new DefaultRedactionService();
        var logEvent = new LogEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            EventTypes.WindowActivated,
            CollectorIds.ForegroundWindow,
            EventSeverity.Information,
            "1",
            "machine",
            """{"schemaVersion":1,"platform":"windows","data":{"processName":"code","windowTitle":"abcdef"}}""");

        var redacted = await service.RedactAsync(logEvent, CancellationToken.None);

        Assert.Equal(logEvent, redacted);
    }

    [Fact]
    public async Task LogRetentionService_Deletes_Events_Older_Than_Retention_Window()
    {
        var now = DateTimeOffset.Parse("2026-01-10T00:00:00Z");
        var repository = new InMemoryLogEventRepository(
        [
            new LogEvent(Guid.NewGuid(), now.AddDays(-10), EventTypes.SystemStartup, CollectorIds.SystemLifecycle, EventSeverity.Information, "1", "m", """{"schemaVersion":1,"platform":"windows","data":{}}"""),
            new LogEvent(Guid.NewGuid(), now.AddDays(-2), EventTypes.SystemStartup, CollectorIds.SystemLifecycle, EventSeverity.Information, "1", "m", """{"schemaVersion":1,"platform":"windows","data":{}}"""),
        ]);
        var settingsRepository = new InMemoryAppSettingsRepository(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SettingsKeys.RetentionDays] = "7",
        });
        var settingsService = new SettingsService(settingsRepository, Options.Create(new InsighterOptions()));
        var service = new LogRetentionService(repository, settingsService, new ManualClock(now));

        var deleted = await service.DeleteExpiredAsync(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Single(repository.Events);
        Assert.Equal(now.AddDays(-2), repository.Events[0].OccurredAtUtc);
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
        var foregroundSettings = JsonNode.Parse(CollectorConfigDefaults.BuildSettingsJson(CollectorIds.ForegroundWindow)) as JsonObject;
        var clipboardSettings = JsonNode.Parse(CollectorConfigDefaults.BuildSettingsJson(CollectorIds.Clipboard)) as JsonObject;
        var powerSettings = JsonNode.Parse(CollectorConfigDefaults.BuildSettingsJson(CollectorIds.Power)) as JsonObject;
        var networkSettings = JsonNode.Parse(CollectorConfigDefaults.BuildSettingsJson(CollectorIds.NetworkConnectivity)) as JsonObject;

        Assert.NotNull(foregroundSettings);
        Assert.NotNull(clipboardSettings);
        Assert.NotNull(powerSettings);
        Assert.NotNull(networkSettings);

        Assert.Equal(200, foregroundSettings!["titleMaxLength"]?.GetValue<int>());
        Assert.Equal(0, foregroundSettings["processBlacklist"]?.AsArray().Count);
        Assert.Equal(nameof(ClipboardCaptureMode.Preview), clipboardSettings!["captureMode"]?.GetValue<string>());
        Assert.Equal(200, clipboardSettings["maxContentLength"]?.GetValue<int>());
        Assert.Equal(true, clipboardSettings["textOnly"]?.GetValue<bool>());
        Assert.Equal(5, powerSettings!["batteryLevelStepPercent"]?.GetValue<int>());
        Assert.Equal(15, powerSettings["pollIntervalSeconds"]?.GetValue<int>());
        Assert.Equal(5, networkSettings!["pollIntervalSeconds"]?.GetValue<int>());
        Assert.Equal(true, networkSettings["reportActiveNetworkChanges"]?.GetValue<bool>());
    }

    private sealed class InMemoryAppSettingsRepository : IAppSettingsRepository
    {
        public InMemoryAppSettingsRepository(Dictionary<string, string> values)
        {
            Values = values;
        }

        public Dictionary<string, string> Values { get; }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyDictionary<string, string>)Values);

        public Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }

        public Task UpsertManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
        {
            foreach (var pair in values)
            {
                Values[pair.Key] = pair.Value;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryLogEventRepository : ILogEventRepository
    {
        public InMemoryLogEventRepository(IReadOnlyList<LogEvent> events)
        {
            Events = events.ToList();
        }

        public List<LogEvent> Events { get; }

        public Task InsertBatchAsync(IReadOnlyCollection<LogEvent> events, CancellationToken cancellationToken)
        {
            Events.AddRange(events);
            return Task.CompletedTask;
        }

        public Task<PagedResult<LogEvent>> SearchAsync(LogQueryFilter filter, CancellationToken cancellationToken)
            => Task.FromResult(new PagedResult<LogEvent>(Events, 1, Events.Count, Events.Count));

        public Task<LogEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Events.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<LogEvent>> GetRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyList<LogEvent>)Events.Where(x => x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc <= toUtc).ToArray());

        public Task<int> DeleteOlderThanAsync(DateTimeOffset thresholdUtc, CancellationToken cancellationToken)
        {
            var deleted = Events.RemoveAll(x => x.OccurredAtUtc < thresholdUtc);
            return Task.FromResult(deleted);
        }
    }

    private sealed class ManualClock : IClock
    {
        public ManualClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
