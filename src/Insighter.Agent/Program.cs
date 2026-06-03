using Microsoft.Extensions.DependencyInjection;

namespace Insighter.Agent;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        try
        {
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.SystemAware);
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            AgentPaths.EnsureDirectories();

            var services = new ServiceCollection();
            services.AddSingleton<IAgentPortResolver, AgentPortResolver>();
            services.AddSingleton<IAgentRuntimeStateStore, AgentRuntimeStateStore>();
            services.AddSingleton<IAgentBrowserLauncher, AgentBrowserLauncher>();
            services.AddSingleton<IAgentSingleInstanceCoordinator, AgentSingleInstanceCoordinator>();
            services.AddSingleton<IAgentStartupManager, AgentStartupManager>();
            services.AddSingleton<IAgentActivationInfo, AgentActivationInfo>();
            services.AddSingleton<IAgentHostRunner, AgentHostRunner>();

            await using var provider = services.BuildServiceProvider();
            var instanceCoordinator = provider.GetRequiredService<IAgentSingleInstanceCoordinator>();
            var isPrimary = await instanceCoordinator.TryAcquirePrimaryInstanceAsync(CancellationToken.None);
            if (!isPrimary)
            {
                if (provider.GetRequiredService<IAgentActivationInfo>().ShouldOpenUiOnLaunch(args))
                {
                    await instanceCoordinator.SignalOpenUiAsync(CancellationToken.None);
                }

                return;
            }

            var context = new AgentApplicationContext(provider, args);
            System.Windows.Forms.Application.Run(context);
        }
        catch (Exception exception)
        {
            await AgentLog.WriteFatalAsync(exception, args);

            if (!args.Any(arg => string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Insighter Agent failed to start.{Environment.NewLine}{Environment.NewLine}See log: {Path.Combine(AgentPaths.LogsDirectory, "agent-fatal.log")}",
                    "Insighter Agent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            throw;
        }
    }
}
