using Windows.ApplicationModel;

namespace Insighter.Agent;

internal interface IAgentStartupTaskAccessor
{
    Task<IAgentStartupTask?> TryGetTaskAsync();
}

internal interface IAgentStartupTask
{
    StartupTaskState State { get; }

    Task<StartupTaskState> RequestEnableAsync();

    void Disable();
}

internal sealed class AgentStartupTaskAccessor : IAgentStartupTaskAccessor
{
    public async Task<IAgentStartupTask?> TryGetTaskAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync(AgentConstants.StartupTaskId);
            return new WindowsAgentStartupTask(task);
        }
        catch
        {
            return null;
        }
    }

    private sealed class WindowsAgentStartupTask : IAgentStartupTask
    {
        private readonly StartupTask _task;

        public WindowsAgentStartupTask(StartupTask task)
        {
            _task = task;
        }

        public StartupTaskState State => _task.State;

        public async Task<StartupTaskState> RequestEnableAsync() => await _task.RequestEnableAsync();

        public void Disable() => _task.Disable();
    }
}
