using System.Threading.Channels;
using Insighter.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Insighter.Application;

public sealed class EventPipelineHostedService : BackgroundService
{
    private const int MaxBatchSize = 100;

    private readonly Channel<LogEvent> _channel;
    private readonly IMachineContextProvider _machineContextProvider;
    private readonly IUserSessionContextProvider _userSessionContextProvider;
    private readonly IContentValidator _contentValidator;
    private readonly IRedactionService _redactionService;
    private readonly ILogEventRepository _logEventRepository;
    private readonly ILogger<EventPipelineHostedService> _logger;

    public EventPipelineHostedService(
        Channel<LogEvent> channel,
        IMachineContextProvider machineContextProvider,
        IUserSessionContextProvider userSessionContextProvider,
        IContentValidator contentValidator,
        IRedactionService redactionService,
        ILogEventRepository logEventRepository,
        ILogger<EventPipelineHostedService> logger)
    {
        _channel = channel;
        _machineContextProvider = machineContextProvider;
        _userSessionContextProvider = userSessionContextProvider;
        _contentValidator = contentValidator;
        _redactionService = redactionService;
        _logEventRepository = logEventRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _channel.Reader;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var first = await reader.ReadAsync(stoppingToken);
                var batch = new List<LogEvent>(MaxBatchSize)
                {
                    await ProcessAsync(first, stoppingToken),
                };

                var flushAt = DateTimeOffset.UtcNow.AddSeconds(2);
                while (batch.Count < MaxBatchSize && DateTimeOffset.UtcNow < flushAt)
                {
                    if (!reader.TryRead(out var candidate))
                    {
                        try
                        {
                            await Task.Delay(50, stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            break;
                        }

                        continue;
                    }

                    batch.Add(await ProcessAsync(candidate, stoppingToken));
                }

                await _logEventRepository.InsertBatchAsync(batch, stoppingToken);
            }
            catch (ChannelClosedException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing event pipeline batch.");
                await Task.Delay(500, stoppingToken);
            }
        }

        var remaining = new List<LogEvent>();
        while (reader.TryRead(out var pending))
        {
            try
            {
                remaining.Add(await ProcessAsync(pending, CancellationToken.None));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process pending event during shutdown.");
            }
        }

        if (remaining.Count > 0)
        {
            await _logEventRepository.InsertBatchAsync(remaining, CancellationToken.None);
        }
    }

    private async Task<LogEvent> ProcessAsync(LogEvent logEvent, CancellationToken cancellationToken)
    {
        var enriched = logEvent with
        {
            MachineId = string.IsNullOrWhiteSpace(logEvent.MachineId) ? _machineContextProvider.GetMachineId() : logEvent.MachineId,
            UserSessionId = string.IsNullOrWhiteSpace(logEvent.UserSessionId) ? _userSessionContextProvider.GetSessionId() : logEvent.UserSessionId,
        };

        _contentValidator.Validate(enriched);
        return await _redactionService.RedactAsync(enriched, cancellationToken);
    }
}
