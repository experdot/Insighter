using Insighter.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Insighter.Application;

public sealed class CollectorManagerHostedService : BackgroundService
{
    private readonly ICollectorManager _collectorManager;
    private readonly ILogger<CollectorManagerHostedService> _logger;

    public CollectorManagerHostedService(ICollectorManager collectorManager, ILogger<CollectorManagerHostedService> logger)
    {
        _collectorManager = collectorManager;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _collectorManager.InitializeAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _collectorManager.MonitorAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Collector manager monitor loop failed.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _collectorManager.StopAllAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
