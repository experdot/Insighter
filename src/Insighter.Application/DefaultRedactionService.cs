using System.Text.Json.Nodes;
using Insighter.Domain;
using Microsoft.Extensions.Logging;

namespace Insighter.Application;

public sealed class DefaultRedactionService : IRedactionService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<DefaultRedactionService> _logger;

    public DefaultRedactionService(ISettingsService settingsService, ILogger<DefaultRedactionService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<LogEvent> RedactAsync(LogEvent logEvent, CancellationToken cancellationToken)
    {
        JsonObject root;
        try
        {
            root = JsonHelpers.ParseObject(logEvent.Content, nameof(logEvent.Content));
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "Skipping redaction for invalid content on event {EventId}.", logEvent.Id);
            return logEvent;
        }

        if (root["data"] is not JsonObject data)
        {
            return logEvent;
        }

        var settings = await _settingsService.GetSnapshotAsync(cancellationToken);

        if (logEvent.CollectorId == CollectorIds.ForegroundWindow)
        {
            var processName = data["processName"]?.GetValue<string>() ?? string.Empty;
            var blacklist = new HashSet<string>(settings.WindowProcessBlacklist, StringComparer.OrdinalIgnoreCase);
            if (blacklist.Contains(processName))
            {
                data["windowTitle"] = "[REDACTED]";
                data["redacted"] = true;
            }
            else if (data["windowTitle"]?.GetValue<string>() is { } title)
            {
                data["windowTitle"] = Truncate(title, settings.DefaultWindowTitleMaxLength);
            }
        }

        if (logEvent.CollectorId == CollectorIds.Clipboard)
        {
            var mode = data["captureMode"]?.GetValue<string>() ?? settings.DefaultClipboardMode.ToString();
            if (string.Equals(mode, nameof(ClipboardCaptureMode.MetadataOnly), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, nameof(ClipboardCaptureMode.Off), StringComparison.OrdinalIgnoreCase))
            {
                data.Remove("text");
                data.Remove("preview");
            }
            else
            {
                if (data["preview"]?.GetValue<string>() is { } preview)
                {
                    data["preview"] = Truncate(preview, settings.DefaultClipboardMaxLength);
                }

                if (data["text"]?.GetValue<string>() is { } text)
                {
                    data["text"] = Truncate(text, settings.DefaultClipboardMaxLength);
                }
            }
        }

        return logEvent with { Content = root.ToJsonString(JsonHelpers.SerializerOptions) };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (maxLength <= 0 || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
