using Insighter.Application;
using Insighter.Domain;
using Microsoft.Extensions.Logging;

namespace Insighter.Infrastructure.Platform.Windows;

public abstract class BaseCollector : ICollector
{
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private CollectorState _state;
    private CancellationTokenSource? _cts;
    private Task? _execution;

    protected BaseCollector(string collectorId, string displayName, IClock clock, ILogger logger)
    {
        CollectorId = collectorId;
        DisplayName = displayName;
        _clock = clock;
        _logger = logger;
        _state = new CollectorState(collectorId, CollectorStatus.Disabled, null, null, _clock.UtcNow);
    }

    public string CollectorId { get; }

    public string DisplayName { get; }

    public async Task StartAsync(CollectorConfig config, CancellationToken cancellationToken)
    {
        if (_execution is { IsCompleted: false })
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _state = _state with { Status = CollectorStatus.Starting, LastError = null, UpdatedAtUtc = _clock.UtcNow };

        try
        {
            await OnConfigUpdatedAsync(config, cancellationToken);
            _execution = Task.Run(() => RunSafeAsync(_cts.Token), CancellationToken.None);
            _state = _state with { Status = CollectorStatus.Running, LastHeartbeatUtc = _clock.UtcNow, UpdatedAtUtc = _clock.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Collector {CollectorId} failed to start.", CollectorId);
            _state = _state with { Status = CollectorStatus.Faulted, LastError = ex.Message, UpdatedAtUtc = _clock.UtcNow };
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_execution is null)
        {
            _state = _state with { Status = CollectorStatus.Disabled, UpdatedAtUtc = _clock.UtcNow };
            return;
        }

        _state = _state with { Status = CollectorStatus.Stopping, UpdatedAtUtc = _clock.UtcNow };
        _cts?.Cancel();

        try
        {
            await _execution.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Collector {CollectorId} failed during stop.", CollectorId);
        }

        _execution = null;
        _cts?.Dispose();
        _cts = null;
        _state = _state with { Status = CollectorStatus.Disabled, UpdatedAtUtc = _clock.UtcNow };
    }

    public async Task UpdateConfigAsync(CollectorConfig config, CancellationToken cancellationToken)
    {
        await OnConfigUpdatedAsync(config, cancellationToken);
        _state = _state with { UpdatedAtUtc = _clock.UtcNow };
    }

    public ValueTask<CollectorState> GetStatusAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(_state);

    protected void TouchHeartbeat()
        => _state = _state with { LastHeartbeatUtc = _clock.UtcNow, UpdatedAtUtc = _clock.UtcNow };

    protected void SetFault(Exception ex)
        => _state = _state with { Status = CollectorStatus.Faulted, LastError = ex.Message, UpdatedAtUtc = _clock.UtcNow };

    protected abstract Task ExecuteAsync(CancellationToken cancellationToken);

    protected abstract Task OnConfigUpdatedAsync(CollectorConfig config, CancellationToken cancellationToken);

    private async Task RunSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Collector {CollectorId} execution faulted.", CollectorId);
            SetFault(ex);
        }
    }
}
