using System.Diagnostics;

namespace Insighter.Agent;

internal interface IAgentBrowserLauncher
{
    void Open(string url);
}

internal sealed class AgentBrowserLauncher : IAgentBrowserLauncher
{
    public void Open(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
}
