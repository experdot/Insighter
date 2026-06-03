namespace Insighter.Agent;

internal static class AgentPaths
{
    private const string RootOverrideEnvironmentVariable = "INSIGHTER_AGENT_ROOT";

    public static string RootDirectory =>
        Environment.GetEnvironmentVariable(RootOverrideEnvironmentVariable) is { Length: > 0 } overridePath
            ? overridePath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Insighter");

    public static string DataDirectory => Path.Combine(RootDirectory, "data");

    public static string StateDirectory => Path.Combine(RootDirectory, "state");

    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public static string DatabasePath => Path.Combine(DataDirectory, "insighter.db");

    public static string RuntimeStatePath => Path.Combine(StateDirectory, "agent-state.json");

    public static string RootOverrideEnvironmentVariableName => RootOverrideEnvironmentVariable;

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(StateDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
