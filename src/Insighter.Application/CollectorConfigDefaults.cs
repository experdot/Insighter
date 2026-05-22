using System.Text.Json.Nodes;
using Insighter.Domain;

namespace Insighter.Application;

public static class CollectorConfigDefaults
{
    public static string BuildSettingsJson(string collectorId, AppSettingsSnapshot settings)
    {
        JsonObject result = collectorId switch
        {
            var id when id == CollectorIds.SystemLifecycle => [],
            var id when id == CollectorIds.SessionState => [],
            var id when id == CollectorIds.ForegroundWindow => new JsonObject
            {
                ["titleMaxLength"] = settings.DefaultWindowTitleMaxLength,
                ["processBlacklist"] = new JsonArray(settings.WindowProcessBlacklist.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
            },
            var id when id == CollectorIds.Idle => new JsonObject
            {
                ["idleThresholdSeconds"] = 300,
                ["ignoreWhileLocked"] = true,
            },
            var id when id == CollectorIds.Clipboard => new JsonObject
            {
                ["captureMode"] = settings.DefaultClipboardMode.ToString(),
                ["maxContentLength"] = settings.DefaultClipboardMaxLength,
                ["textOnly"] = true,
            },
            var id when id == CollectorIds.ProcessLifecycle => new JsonObject
            {
                ["captureExecutablePath"] = true,
                ["ignoreCurrentProcess"] = true,
                ["ignoreSystemProcesses"] = true,
            },
            var id when id == CollectorIds.Power => new JsonObject
            {
                ["batteryLevelStepPercent"] = 5,
                ["reportPowerSourceChanges"] = true,
                ["reportChargingStateChanges"] = true,
                ["reportBatteryLevelChanges"] = true,
                ["pollIntervalSeconds"] = 15,
            },
            var id when id == CollectorIds.NetworkConnectivity => new JsonObject
            {
                ["pollIntervalSeconds"] = 5,
                ["reportActiveNetworkChanges"] = true,
            },
            _ => [],
        };

        return result.ToJsonString(JsonHelpers.SerializerOptions);
    }
}
