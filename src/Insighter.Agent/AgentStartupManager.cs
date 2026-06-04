namespace Insighter.Agent;

internal interface IAgentStartupManager
{
    Task<AgentStartupRegistrationState> GetStateAsync();

    Task<AgentStartupRegistrationState> EnableAsync();

    Task<AgentStartupRegistrationState> DisableAsync();
}

internal sealed class AgentStartupManager : IAgentStartupManager
{
    private readonly IAgentStartupTaskAccessor _taskAccessor;

    public AgentStartupManager(IAgentStartupTaskAccessor taskAccessor)
    {
        _taskAccessor = taskAccessor;
    }

    public async Task<AgentStartupRegistrationState> GetStateAsync()
    {
        var task = await _taskAccessor.TryGetTaskAsync();
        return CreateState(task);
    }

    public async Task<AgentStartupRegistrationState> EnableAsync()
    {
        var task = await _taskAccessor.TryGetTaskAsync();
        if (task is null)
        {
            return AgentStartupRegistrationState.Unavailable;
        }

        var state = await task.RequestEnableAsync();
        return new AgentStartupRegistrationState(true, state);
    }

    public async Task<AgentStartupRegistrationState> DisableAsync()
    {
        var task = await _taskAccessor.TryGetTaskAsync();
        if (task is null)
        {
            return AgentStartupRegistrationState.Unavailable;
        }

        task.Disable();
        return CreateState(task);
    }

    private static AgentStartupRegistrationState CreateState(IAgentStartupTask? task) =>
        task is null
            ? AgentStartupRegistrationState.Unavailable
            : new AgentStartupRegistrationState(true, task.State);
}
