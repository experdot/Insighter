using System.Text.Json;

namespace Insighter.Agent;

internal static class AgentJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}
