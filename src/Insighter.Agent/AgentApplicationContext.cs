using System.Diagnostics;
using System.Drawing;
using Microsoft.Extensions.DependencyInjection;
using Windows.ApplicationModel;

namespace Insighter.Agent;

internal sealed class AgentApplicationContext : ApplicationContext
{
    private readonly IAgentHostRunner _hostRunner;
    private readonly IAgentSingleInstanceCoordinator _instanceCoordinator;
    private readonly IAgentBrowserLauncher _browserLauncher;
    private readonly IAgentRuntimeStateStore _runtimeStateStore;
    private readonly IAgentStartupManager _startupManager;
    private readonly IAgentActivationInfo _activationInfo;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _openInsighterMenuItem;
    private readonly ToolStripMenuItem _launchAtLoginMenuItem;
    private readonly System.Windows.Forms.Timer _startupTimer;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly string[] _args;
    private readonly Icon _trayIcon;
    private bool _isInitialized;
    private bool _isShuttingDown;

    public AgentApplicationContext(IServiceProvider services, string[] args)
    {
        _args = args;
        _hostRunner = services.GetRequiredService<IAgentHostRunner>();
        _instanceCoordinator = services.GetRequiredService<IAgentSingleInstanceCoordinator>();
        _browserLauncher = services.GetRequiredService<IAgentBrowserLauncher>();
        _runtimeStateStore = services.GetRequiredService<IAgentRuntimeStateStore>();
        _startupManager = services.GetRequiredService<IAgentStartupManager>();
        _activationInfo = services.GetRequiredService<IAgentActivationInfo>();
        _trayIcon = LoadTrayIcon();

        _openInsighterMenuItem = new ToolStripMenuItem("Open Insighter", null, (_, _) => _ = OpenUiAsync())
        {
            Enabled = false,
        };
        _launchAtLoginMenuItem = new ToolStripMenuItem("Launch at login")
        {
            CheckOnClick = false,
            Enabled = false,
        };
        _launchAtLoginMenuItem.Click += async (_, _) => await ToggleStartupAsync();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_openInsighterMenuItem);
        menu.Items.Add(_launchAtLoginMenuItem);
        menu.Items.Add(new ToolStripMenuItem("Open data folder", null, (_, _) => OpenDataFolder()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, async (_, _) => await ExitAgentAsync()));

        _notifyIcon = new NotifyIcon
        {
            Text = "Insighter Agent",
            Visible = false,
            ContextMenuStrip = menu,
            Icon = _trayIcon,
        };
        _notifyIcon.DoubleClick += (_, _) => _ = OpenUiAsync();

        _startupTimer = new System.Windows.Forms.Timer
        {
            Interval = 1,
            Enabled = true,
        };
        _startupTimer.Tick += OnStartupTimerTick;
    }

    private async Task HandleCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (string.Equals(command, AgentConstants.OpenUiCommand, StringComparison.OrdinalIgnoreCase))
        {
            _notifyIcon.ContextMenuStrip?.BeginInvoke(new Action(() => _ = OpenUiAsync()));
        }
    }

    private async Task OpenUiAsync()
    {
        if (!_isInitialized)
        {
            return;
        }

        try
        {
            var state = await _runtimeStateStore.ReadAsync(CancellationToken.None);
            var url = state?.ApiBaseUrl ?? _hostRunner.ApiBaseUrl;
            _browserLauncher.Open(url);
        }
        catch (Exception exception)
        {
            await AgentLog.WriteFatalAsync(exception, _args);
        }
    }

    private async void OnStartupTimerTick(object? sender, EventArgs e)
    {
        _startupTimer.Stop();
        _startupTimer.Tick -= OnStartupTimerTick;

        try
        {
            await _hostRunner.StartAsync(_lifetimeCts.Token);
            _ = Task.Run(() => _instanceCoordinator.RunServerAsync(HandleCommandAsync, _lifetimeCts.Token));
            await RefreshStartupMenuStateAsync();
            _notifyIcon.Visible = true;
            _openInsighterMenuItem.Enabled = true;
            _isInitialized = true;

            if (_activationInfo.ShouldOpenUiOnLaunch(_args))
            {
                _ = OpenUiAsync();
            }
        }
        catch (Exception exception)
        {
            await AgentLog.WriteFatalAsync(exception, _args);

            if (!_args.Any(arg => string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    $"Insighter Agent failed to start.{Environment.NewLine}{Environment.NewLine}See log: {Path.Combine(AgentPaths.LogsDirectory, "agent-fatal.log")}",
                    "Insighter Agent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            await ExitAgentAsync();
        }
    }

    private static void OpenDataFolder()
    {
        AgentPaths.EnsureDirectories();
        Process.Start(new ProcessStartInfo
        {
            FileName = AgentPaths.RootDirectory,
            UseShellExecute = true,
        });
    }

    private async Task ToggleStartupAsync()
    {
        var state = await _startupManager.GetStateAsync();
        if (state is null)
        {
            await RefreshStartupMenuStateAsync();
            return;
        }

        if (state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
        {
            await _startupManager.DisableAsync();
        }
        else
        {
            await _startupManager.EnableAsync();
        }

        await RefreshStartupMenuStateAsync();
    }

    private async Task RefreshStartupMenuStateAsync()
    {
        var state = await _startupManager.GetStateAsync();
        _launchAtLoginMenuItem.Checked = state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        _launchAtLoginMenuItem.Enabled = state is not StartupTaskState.EnabledByPolicy;
    }

    private static Icon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcon.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private async Task ExitAgentAsync()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        _notifyIcon.Visible = false;
        _lifetimeCts.Cancel();

        try
        {
            await _hostRunner.StopAsync(CancellationToken.None);
            await _instanceCoordinator.DisposeAsync();
        }
        finally
        {
            _startupTimer.Dispose();
            _notifyIcon.Dispose();
            _trayIcon.Dispose();
            _lifetimeCts.Dispose();
            ExitThread();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isShuttingDown)
        {
            ExitAgentAsync().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }
}
