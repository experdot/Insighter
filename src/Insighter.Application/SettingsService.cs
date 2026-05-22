using System.Text.Json;
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
            GetClipboardMode(persisted, baseOptions.DefaultClipboardMode),
            GetInt(persisted, SettingsKeys.DefaultClipboardMaxLength, baseOptions.DefaultClipboardMaxLength),
            GetInt(persisted, SettingsKeys.DefaultWindowTitleMaxLength, baseOptions.DefaultWindowTitleMaxLength),
            GetStringList(persisted, SettingsKeys.WindowProcessBlacklist, baseOptions.WindowProcessBlacklist),
            baseOptions.AutoStartCollectors);
    }

    public async Task<AppSettingsSnapshot> UpdateAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        await _repository.UpsertManyAsync(values, cancellationToken);
        return await GetSnapshotAsync(cancellationToken);
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
        => values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static ClipboardCaptureMode GetClipboardMode(IReadOnlyDictionary<string, string> values, string fallback)
    {
        var source = values.TryGetValue(SettingsKeys.DefaultClipboardMode, out var value) ? value : fallback;
        return Enum.TryParse<ClipboardCaptureMode>(source, true, out var parsed) ? parsed : ClipboardCaptureMode.Preview;
    }

    private static IReadOnlyList<string> GetStringList(IReadOnlyDictionary<string, string> values, string key, IReadOnlyList<string> fallback)
    {
        if (!values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(raw, JsonHelpers.SerializerOptions) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
