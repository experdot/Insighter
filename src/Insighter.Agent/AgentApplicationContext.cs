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
    private readonly IAgentOpenRequestStore _openRequestStore;
    private readonly IAgentRuntimeStateStore _runtimeStateStore;
    private readonly IAgentStartupManager _startupManager;
    private readonly IAgentActivationInfo _activationInfo;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _openInsighterMenuItem;
    private readonly ToolStripMenuItem _launchAtLoginMenuItem;
    private readonly Control _uiDispatcher;
    private readonly System.Windows.Forms.Timer _startupTimer;
    private readonly System.Windows.Forms.Timer _pendingOpenRequestTimer;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly string[] _args;
    private readonly Icon _trayIcon;
    private Task? _commandServerTask;
    private bool _isInitialized;
    private bool _isShuttingDown;
    private bool _pendingOpenUi;

    public AgentApplicationContext(IServiceProvider services, string[] args)
    {
        _args = args;
        _hostRunner = services.GetRequiredService<IAgentHostRunner>();
        _instanceCoordinator = services.GetRequiredService<IAgentSingleInstanceCoordinator>();
        _browserLauncher = services.GetRequiredService<IAgentBrowserLauncher>();
        _openRequestStore = services.GetRequiredService<IAgentOpenRequestStore>();
        _runtimeStateStore = services.GetRequiredService<IAgentRuntimeStateStore>();
        _startupManager = services.GetRequiredService<IAgentStartupManager>();
        _activationInfo = services.GetRequiredService<IAgentActivationInfo>();
        _trayIcon = LoadTrayIcon();

        _openInsighterMenuItem = new ToolStripMenuItem("Open Insighter", null, (_, _) => _ = OpenUiAsync())
        {
            Enabled = false,
        };
        _launchAtLoginMenuItem = new ToolStripMenuItem("Launch at login (MSIX only)")
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
        _uiDispatcher = new Control();
        _ = _uiDispatcher.Handle;

        _startupTimer = new System.Windows.Forms.Timer
        {
            Interval = 1,
            Enabled = true,
        };
        _startupTimer.Tick += OnStartupTimerTick;

        _pendingOpenRequestTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000,
            Enabled = true,
        };
        _pendingOpenRequestTimer.Tick += OnPendingOpenRequestTimerTick;

        _commandServerTask = Task.Run(() => _instanceCoordinator.RunServerAsync(HandleCommandAsync, _lifetimeCts.Token));
    }

    private Task HandleCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (string.Equals(command, AgentConstants.OpenUiCommand, StringComparison.OrdinalIgnoreCase))
        {
            BeginInvokeOnUiThread(RequestOpenUi);
        }

        return Task.CompletedTask;
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

    private void RequestOpenUi()
    {
        _pendingOpenUi = true;

        if (_isInitialized)
        {
            _ = OpenPendingUiAsync();
        }
    }

    private async Task OpenPendingUiAsync()
    {
        if (!_isInitialized || !_pendingOpenUi)
        {
            return;
        }

        _pendingOpenUi = false;
        _openRequestStore.Clear();
        await OpenUiAsync();
    }

    private async void OnStartupTimerTick(object? sender, EventArgs e)
    {
        _startupTimer.Stop();
        _startupTimer.Tick -= OnStartupTimerTick;

        try
        {
            _pendingOpenUi = _activationInfo.ShouldOpenUiOnLaunch(_args);
            _pendingOpenUi |= _openRequestStore.ConsumePending();

            await _hostRunner.StartAsync(_lifetimeCts.Token);
            await RefreshStartupMenuStateAsync();
            _notifyIcon.Visible = true;
            _openInsighterMenuItem.Enabled = true;
            _isInitialized = true;
            await OpenPendingUiAsync();
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

    private void OnPendingOpenRequestTimerTick(object? sender, EventArgs e)
    {
        if (_openRequestStore.ConsumePending())
        {
            RequestOpenUi();
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
        if (!state.IsAvailable || state.IsPolicyManaged)
        {
            await RefreshStartupMenuStateAsync();
            return;
        }

        if (state.IsEnabled)
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
        _launchAtLoginMenuItem.Text = state.IsAvailable ? "Launch at login" : "Launch at login (MSIX only)";
        _launchAtLoginMenuItem.Checked = state.IsEnabled;
        _launchAtLoginMenuItem.Enabled = state.IsAvailable && !state.IsPolicyManaged;
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
            if (_commandServerTask is not null)
            {
                await _commandServerTask;
            }

            await _instanceCoordinator.DisposeAsync();
        }
        finally
        {
            _startupTimer.Dispose();
            _pendingOpenRequestTimer.Dispose();
            _notifyIcon.Dispose();
            _uiDispatcher.Dispose();
            _trayIcon.Dispose();
            _lifetimeCts.Dispose();
            ExitThread();
        }
    }

    private void BeginInvokeOnUiThread(Action action)
    {
        if (_uiDispatcher.IsDisposed)
        {
            return;
        }

        if (_uiDispatcher.InvokeRequired)
        {
            _uiDispatcher.BeginInvoke(action);
            return;
        }

        action();
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
