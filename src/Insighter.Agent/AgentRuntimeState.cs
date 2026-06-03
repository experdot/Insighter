namespace Insighter.Agent;

internal sealed class AgentRuntimeState
{
    public string ApiBaseUrl { get; init; } = "http://127.0.0.1:5078";

    public int ProcessId { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}
