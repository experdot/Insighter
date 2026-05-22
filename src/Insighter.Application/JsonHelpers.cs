using System.Text.Json;
using System.Text.Json.Nodes;

namespace Insighter.Application;

public static class JsonHelpers
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static JsonObject ParseObject(string json, string fieldName)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj)
            {
                return obj;
            }
        }
        catch (JsonException)
        {
        }

        throw new ApiException(System.Net.HttpStatusCode.BadRequest, "invalid_json", $"{fieldName} must be a valid JSON object.");
    }

    public static string Serialize(object value) => JsonSerializer.Serialize(value, SerializerOptions);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, SerializerOptions);
}
