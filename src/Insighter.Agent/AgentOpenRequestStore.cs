namespace Insighter.Agent;

internal interface IAgentOpenRequestStore
{
    void MarkPending();

    bool ConsumePending();

    void Clear();
}

internal sealed class AgentOpenRequestStore : IAgentOpenRequestStore
{
    public void MarkPending()
    {
        AgentPaths.EnsureDirectories();
        File.WriteAllText(AgentPaths.PendingOpenUiPath, DateTimeOffset.UtcNow.ToString("O"));
    }

    public bool ConsumePending()
    {
        if (!File.Exists(AgentPaths.PendingOpenUiPath))
        {
            return false;
        }

        File.Delete(AgentPaths.PendingOpenUiPath);
        return true;
    }

    public void Clear()
    {
        if (File.Exists(AgentPaths.PendingOpenUiPath))
        {
            File.Delete(AgentPaths.PendingOpenUiPath);
        }
    }
}
