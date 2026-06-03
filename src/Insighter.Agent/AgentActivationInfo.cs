namespace Insighter.Agent;

internal interface IAgentActivationInfo
{
    bool ShouldOpenUiOnLaunch(string[] args);
}

internal sealed class AgentActivationInfo : IAgentActivationInfo
{
    public bool ShouldOpenUiOnLaunch(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--open-ui", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (args.Any(arg => string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }
}
