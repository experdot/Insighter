using Insighter.Domain;
using Microsoft.Extensions.Options;

namespace Insighter.Application;

public sealed class SettingsService : ISettingsService
{
    private readonly IAppSettingsRepository _repository;
    private readonly IOptions<InsighterOptions> _options;

    public SettingsService(IAppSettingsRepository repository, IOptions<InsighterOptions> options)
    {
        _repository = repository;
        _options = options;
    }

    public async Task<AppSettingsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var persisted = await _repository.GetAllAsync(cancellationToken);
        var baseOptions = _options.Value;

        var databasePath = string.IsNullOrWhiteSpace(baseOptions.DatabasePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Insighter", "insighter.db")
            : baseOptions.DatabasePath;

        return new AppSettingsSnapshot(
            databasePath,
            baseOptions.ApiUrls,
            baseOptions.QueueCapacity,
            GetInt(persisted, SettingsKeys.RetentionDays, baseOptions.RetentionDays),
            baseOptions.QueryMaxPageSize,
            baseOptions.AutoStartCollectors);
    }

    public async Task<AppSettingsSnapshot> UpdateAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        await _repository.UpsertManyAsync(values, cancellationToken);
        return await GetSnapshotAsync(cancellationToken);
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
        => values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

}
