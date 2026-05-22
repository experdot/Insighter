using System.Text.Json.Nodes;
using Insighter.Domain;

namespace Insighter.Application;

public sealed class JsonContentValidator : IContentValidator
{
    public void Validate(LogEvent logEvent)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(logEvent.Content);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Log event {logEvent.Id} content is not valid JSON.", ex);
        }

        if (node is not JsonObject content)
        {
            throw new InvalidOperationException($"Log event {logEvent.Id} content must be a JSON object.");
        }

        if (content["schemaVersion"]?.GetValue<int?>() is null)
        {
            throw new InvalidOperationException($"Log event {logEvent.Id} content is missing schemaVersion.");
        }

        if (string.IsNullOrWhiteSpace(content["platform"]?.GetValue<string>()))
        {
            throw new InvalidOperationException($"Log event {logEvent.Id} content is missing platform.");
        }

        if (content["data"] is not JsonObject)
        {
            throw new InvalidOperationException($"Log event {logEvent.Id} content is missing data object.");
        }
    }
}
