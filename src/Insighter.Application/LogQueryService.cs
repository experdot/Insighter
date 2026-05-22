using System.Net;
using Insighter.Domain;
using Microsoft.Extensions.Logging;

namespace Insighter.Application;

public sealed class LogQueryService : ILogQueryService
{
    private readonly ILogEventRepository _repository;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<LogQueryService> _logger;

    public LogQueryService(ILogEventRepository repository, ISettingsService settingsService, ILogger<LogQueryService> logger)
    {
        _repository = repository;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<PagedResult<LogEvent>> SearchAsync(LogQueryFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Page <= 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_page", "Page must be greater than zero.");
        }

        if (filter.PageSize <= 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "invalid_page_size", "Page size must be greater than zero.");
        }

        var settings = await _settingsService.GetSnapshotAsync(cancellationToken);
        var pageSize = Math.Min(filter.PageSize, settings.QueryMaxPageSize);
        var effectiveFilter = filter with { PageSize = pageSize };
        _logger.LogDebug("Searching logs with page {Page} size {PageSize}.", filter.Page, pageSize);
        return await _repository.SearchAsync(effectiveFilter, cancellationToken);
    }

    public Task<LogEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _repository.GetByIdAsync(id, cancellationToken);
}
