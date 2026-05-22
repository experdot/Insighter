using System.Diagnostics;
using Insighter.Domain;

namespace Insighter.Application;

public sealed class DefaultMachineContextProvider : IMachineContextProvider
{
    public string GetMachineId() => Environment.MachineName;

    public string GetPlatform() => OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
}

public sealed class DefaultUserSessionContextProvider : IUserSessionContextProvider
{
    public string GetSessionId()
    {
        try
        {
            return Process.GetCurrentProcess().SessionId.ToString();
        }
        catch
        {
            return "0";
        }
    }
}
