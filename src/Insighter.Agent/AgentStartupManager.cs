using Windows.ApplicationModel;

namespace Insighter.Agent;

internal interface IAgentStartupManager
{
    Task<StartupTaskState?> GetStateAsync();

    Task<StartupTaskState?> EnableAsync();

    Task DisableAsync();
}

internal sealed class AgentStartupManager : IAgentStartupManager
{
    public async Task<StartupTaskState?> GetStateAsync()
    {
        var task = await TryGetTaskAsync();
        return task?.State;
    }

    public async Task<StartupTaskState?> EnableAsync()
    {
        var task = await TryGetTaskAsync();
        if (task is null)
        {
            return null;
        }

        return await task.RequestEnableAsync();
    }

    public async Task DisableAsync()
    {
        var task = await TryGetTaskAsync();
        task?.Disable();
    }

    private static async Task<StartupTask?> TryGetTaskAsync()
    {
        try
        {
            return await StartupTask.GetAsync(AgentConstants.StartupTaskId);
        }
        catch
        {
            return null;
        }
    }
}
