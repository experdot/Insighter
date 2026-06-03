using System.Diagnostics;

var agentPath = Path.Combine(AppContext.BaseDirectory, "Insighter.Agent.exe");
if (!File.Exists(agentPath))
{
    return;
}

Process.Start(new ProcessStartInfo
{
    FileName = agentPath,
    Arguments = "--background",
    WorkingDirectory = AppContext.BaseDirectory,
    UseShellExecute = true,
});
