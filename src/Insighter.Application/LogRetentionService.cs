using Insighter.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Insighter.Application;

public sealed class LogRetentionService : ILogRetentionService
{
    private readonly ILogEventRepository _repository;
    private readonly ISettingsService _settingsService;
    private readonly IClock _clock;

    public LogRetentionService(ILogEventRepository repository, ISettingsService settingsService, IClock clock)
    {
        _repository = repository;
        _settingsService = settingsService;
        _clock = clock;
    }

    public async Task<int> DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetSnapshotAsync(cancellationToken);
        if (settings.RetentionDays <= 0)
        {
            return 0;
        }

        var thresholdUtc = _clock.UtcNow.AddDays(-settings.RetentionDays);
        return await _repository.DeleteOlderThanAsync(thresholdUtc, cancellationToken);
    }
}

public sealed class RetentionCleanupHostedService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    private readonly ILogRetentionService _retentionService;
    private readonly ILogger<RetentionCleanupHostedService> _logger;

    public RetentionCleanupHostedService(
        ILogRetentionService retentionService,
        ILogger<RetentionCleanupHostedService> logger)
    {
        _retentionService = retentionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanupAsync(stoppingToken);

        using var timer = new PeriodicTimer(CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _retentionService.DeleteExpiredAsync(cancellationToken);
            if (deleted > 0)
            {
                _logger.LogInformation("Deleted {DeletedCount} expired log events.", deleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log retention cleanup failed.");
        }
    }
}
