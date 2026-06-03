using System.Text.Json;

namespace Insighter.Agent;

internal interface IAgentRuntimeStateStore
{
    Task WriteAsync(AgentRuntimeState state, CancellationToken cancellationToken);

    Task<AgentRuntimeState?> ReadAsync(CancellationToken cancellationToken);

    void Delete();
}

internal sealed class AgentRuntimeStateStore : IAgentRuntimeStateStore
{
    public async Task WriteAsync(AgentRuntimeState state, CancellationToken cancellationToken)
    {
        AgentPaths.EnsureDirectories();
        await using var stream = File.Create(AgentPaths.RuntimeStatePath);
        await JsonSerializer.SerializeAsync(stream, state, AgentJson.SerializerOptions, cancellationToken);
    }

    public async Task<AgentRuntimeState?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(AgentPaths.RuntimeStatePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(AgentPaths.RuntimeStatePath);
        return await JsonSerializer.DeserializeAsync<AgentRuntimeState>(stream, AgentJson.SerializerOptions, cancellationToken);
    }

    public void Delete()
    {
        if (File.Exists(AgentPaths.RuntimeStatePath))
        {
            File.Delete(AgentPaths.RuntimeStatePath);
        }
    }
}
