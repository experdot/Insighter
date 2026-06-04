using Windows.ApplicationModel;

namespace Insighter.Agent;

internal sealed class AgentStartupRegistrationState
{
    public static AgentStartupRegistrationState Unavailable { get; } = new(false, null);

    public AgentStartupRegistrationState(bool isAvailable, StartupTaskState? taskState)
    {
        IsAvailable = isAvailable;
        TaskState = taskState;
    }

    public bool IsAvailable { get; }

    public StartupTaskState? TaskState { get; }

    public bool IsEnabled => TaskState is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

    public bool IsPolicyManaged => TaskState is StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy;
}
