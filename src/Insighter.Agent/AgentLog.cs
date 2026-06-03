using System.Text;

namespace Insighter.Agent;

internal static class AgentLog
{
    public static async Task WriteFatalAsync(Exception exception, string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            AgentPaths.EnsureDirectories();
            var logPath = Path.Combine(AgentPaths.LogsDirectory, "agent-fatal.log");
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTimeOffset.UtcNow:O}] Insighter Agent fatal startup error");
            builder.AppendLine($"Args: {string.Join(' ', args)}");
            builder.AppendLine(exception.ToString());
            builder.AppendLine();
            await File.AppendAllTextAsync(logPath, builder.ToString(), cancellationToken);
        }
        catch
        {
        }
    }
}
