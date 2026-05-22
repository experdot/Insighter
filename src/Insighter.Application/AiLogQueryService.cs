using Insighter.Domain;

namespace Insighter.Application;

public sealed class AiLogQueryService : IAiLogQueryService
{
    private readonly ILogQueryService _logQueryService;

    public AiLogQueryService(ILogQueryService logQueryService)
    {
        _logQueryService = logQueryService;
    }

    public async Task<IReadOnlyList<LogEvent>> SearchLogsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int maxResults,
        string? eventType,
        CancellationToken cancellationToken)
    {
        var result = await _logQueryService.SearchAsync(
            new LogQueryFilter(fromUtc, toUtc, eventType, null, null, null, null, 1, Math.Min(Math.Max(maxResults, 1), 200)),
            cancellationToken);

        return result.Items;
    }
}
