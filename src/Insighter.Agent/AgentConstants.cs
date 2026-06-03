namespace Insighter.Agent;

internal static class AgentConstants
{
    public const string MutexName = @"Local\Insighter.Agent";
    public const string PipeName = "Insighter.Agent.Control";
    public const string StartupTaskId = "InsighterAgentStartup";
    public const string OpenUiCommand = "open-ui";
}
