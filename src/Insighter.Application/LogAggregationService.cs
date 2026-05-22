using Insighter.Domain;

namespace Insighter.Application;

public sealed class LogAggregationService : ILogAggregationService
{
    private readonly ILogEventRepository _repository;

    public LogAggregationService(ILogEventRepository repository)
    {
        _repository = repository;
    }

    public async Task<DashboardSummary> GetSummaryAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        var events = (await _repository.GetRangeAsync(fromUtc, toUtc, cancellationToken)).OrderBy(x => x.OccurredAtUtc).ToArray();
        var counts = events
            .GroupBy(x => x.EventType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        return new DashboardSummary(
            TotalEvents: events.Length,
            WindowActivationCount: counts.GetValueOrDefault(EventTypes.WindowActivated),
            ClipboardChangeCount: counts.GetValueOrDefault(EventTypes.ClipboardChanged),
            IdlePeriodCount: counts.GetValueOrDefault(EventTypes.UserIdleStarted),
            TotalIdleDurationSeconds: CalculateIdleSeconds(events, toUtc),
            EventCountsByType: counts);
    }

    public async Task<IReadOnlyList<TimelineBucket>> GetTimelineAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, string bucket, CancellationToken cancellationToken)
    {
        var events = await _repository.GetRangeAsync(fromUtc, toUtc, cancellationToken);
        var bucketSize = string.Equals(bucket, "day", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromDays(1)
            : TimeSpan.FromHours(1);

        var results = events
            .GroupBy(x => Align(x.OccurredAtUtc, bucketSize))
            .OrderBy(x => x.Key)
            .Select(group => new TimelineBucket(
                group.Key,
                group.Key.Add(bucketSize),
                group.Count(),
                group.GroupBy(x => x.EventType, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        return results;
    }

    private static DateTimeOffset Align(DateTimeOffset value, TimeSpan bucket)
    {
        var utc = value.ToUniversalTime();
        if (bucket == TimeSpan.FromDays(1))
        {
            return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
        }

        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    private static double CalculateIdleSeconds(IReadOnlyList<LogEvent> events, DateTimeOffset rangeEndUtc)
    {
        DateTimeOffset? idleStartedAt = null;
        double totalSeconds = 0;

        foreach (var logEvent in events)
        {
            if (logEvent.EventType == EventTypes.UserIdleStarted)
            {
                idleStartedAt ??= logEvent.OccurredAtUtc;
            }
            else if (logEvent.EventType == EventTypes.UserIdleEnded && idleStartedAt is { } startedAt)
            {
                totalSeconds += Math.Max(0, (logEvent.OccurredAtUtc - startedAt).TotalSeconds);
                idleStartedAt = null;
            }
        }

        if (idleStartedAt is { } unfinished)
        {
            totalSeconds += Math.Max(0, (rangeEndUtc - unfinished).TotalSeconds);
        }

        return totalSeconds;
    }
}
