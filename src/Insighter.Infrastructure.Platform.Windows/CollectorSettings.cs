using System.Text.Json.Serialization;

namespace Insighter.Infrastructure.Platform.Windows;

public sealed class ForegroundWindowCollectorSettings
{
    [JsonPropertyName("titleMaxLength")]
    public int TitleMaxLength { get; set; } = 200;

    [JsonPropertyName("processBlacklist")]
    public string[] ProcessBlacklist { get; set; } = [];
}

public sealed class IdleCollectorSettings
{
    [JsonPropertyName("idleThresholdSeconds")]
    public int IdleThresholdSeconds { get; set; } = 300;

    [JsonPropertyName("ignoreWhileLocked")]
    public bool IgnoreWhileLocked { get; set; } = true;
}

public sealed class ClipboardCollectorSettings
{
    [JsonPropertyName("captureMode")]
    public string CaptureMode { get; set; } = "Preview";

    [JsonPropertyName("maxContentLength")]
    public int MaxContentLength { get; set; } = 200;

    [JsonPropertyName("textOnly")]
    public bool TextOnly { get; set; } = true;
}

public sealed class PowerCollectorSettings
{
    [JsonPropertyName("batteryLevelStepPercent")]
    public int BatteryLevelStepPercent { get; set; } = 5;

    [JsonPropertyName("reportPowerSourceChanges")]
    public bool ReportPowerSourceChanges { get; set; } = true;

    [JsonPropertyName("reportChargingStateChanges")]
    public bool ReportChargingStateChanges { get; set; } = true;

    [JsonPropertyName("reportBatteryLevelChanges")]
    public bool ReportBatteryLevelChanges { get; set; } = true;

    [JsonPropertyName("pollIntervalSeconds")]
    public int PollIntervalSeconds { get; set; } = 15;
}

public sealed class NetworkConnectivityCollectorSettings
{
    [JsonPropertyName("pollIntervalSeconds")]
    public int PollIntervalSeconds { get; set; } = 5;

    [JsonPropertyName("reportActiveNetworkChanges")]
    public bool ReportActiveNetworkChanges { get; set; } = true;
}
