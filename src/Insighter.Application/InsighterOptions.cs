using Insighter.Domain;

namespace Insighter.Application;

public sealed class InsighterOptions
{
    public const string SectionName = "Insighter";

    public string? DatabasePath { get; set; }

    public string ApiUrls { get; set; } = "http://127.0.0.1:5078";

    public int QueueCapacity { get; set; } = 4096;

    public int RetentionDays { get; set; } = 30;

    public int QueryMaxPageSize { get; set; } = 500;

    public string DefaultClipboardMode { get; set; } = nameof(ClipboardCaptureMode.Preview);

    public int DefaultClipboardMaxLength { get; set; } = 200;

    public int DefaultWindowTitleMaxLength { get; set; } = 200;

    public string[] WindowProcessBlacklist { get; set; } = [];

    public bool AutoStartCollectors { get; set; } = true;
}
