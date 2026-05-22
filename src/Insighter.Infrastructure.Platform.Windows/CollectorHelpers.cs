using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Insighter.Application;

namespace Insighter.Infrastructure.Platform.Windows;

internal static class CollectorHelpers
{
    public static string BuildContent(string platform, JsonObject data)
        => new JsonObject
        {
            ["schemaVersion"] = 1,
            ["platform"] = platform,
            ["data"] = data,
        }.ToJsonString(JsonHelpers.SerializerOptions);

    public static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    public static string NormalizeProcessName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var fileName = Path.GetFileNameWithoutExtension(value);
        return string.IsNullOrWhiteSpace(fileName) ? value : fileName;
    }
}
