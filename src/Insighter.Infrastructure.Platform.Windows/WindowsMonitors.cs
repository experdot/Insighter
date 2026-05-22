using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Insighter.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Insighter.Infrastructure.Platform.Windows;

public sealed class WindowsForegroundWindowMonitor : IForegroundWindowMonitor
{
    public ValueTask<ForegroundWindowSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var handle = NativeMethods.GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return ValueTask.FromResult<ForegroundWindowSnapshot?>(null);
        }

        NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        var title = GetWindowTitle(handle);

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return ValueTask.FromResult<ForegroundWindowSnapshot?>(new ForegroundWindowSnapshot(handle, (int)processId, process.ProcessName, title));
        }
        catch
        {
            return ValueTask.FromResult<ForegroundWindowSnapshot?>(new ForegroundWindowSnapshot(handle, (int)processId, processId.ToString(), title));
        }
    }

    private static string GetWindowTitle(nint handle)
    {
        var builder = new StringBuilder(1024);
        _ = NativeMethods.GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }
}

public sealed class WindowsIdleMonitor : IIdleMonitor
{
    public ValueTask<TimeSpan> GetIdleTimeAsync(CancellationToken cancellationToken)
    {
        var lastInput = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>(),
        };

        if (!NativeMethods.GetLastInputInfo(ref lastInput))
        {
            return ValueTask.FromResult(TimeSpan.Zero);
        }

        var tickCount = Environment.TickCount64;
        var idleMs = Math.Max(0, tickCount - lastInput.dwTime);
        return ValueTask.FromResult(TimeSpan.FromMilliseconds(idleMs));
    }
}

public sealed class WindowsSessionStateMonitor : ISessionStateMonitor
{
    private readonly ILogger<WindowsSessionStateMonitor> _logger;
    private volatile SessionLockState _current = SessionLockState.Unknown;

    public WindowsSessionStateMonitor(ILogger<WindowsSessionStateMonitor> logger)
    {
        _logger = logger;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public ValueTask<SessionLockState> GetCurrentAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(_current);

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        _current = e.Reason switch
        {
            SessionSwitchReason.SessionLock => SessionLockState.Locked,
            SessionSwitchReason.SessionUnlock => SessionLockState.Unlocked,
            _ => _current,
        };

        _logger.LogDebug("Session state changed to {State}.", _current);
    }
}

public sealed class WindowsClipboardMonitor : IClipboardMonitor
{
    private readonly ILogger<WindowsClipboardMonitor> _logger;

    public WindowsClipboardMonitor(ILogger<WindowsClipboardMonitor> logger)
    {
        _logger = logger;
    }

    public ValueTask<ClipboardSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var sequence = NativeMethods.GetClipboardSequenceNumber();
        var hasText = NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_UNICODETEXT);
        if (!hasText)
        {
            return ValueTask.FromResult<ClipboardSnapshot?>(new ClipboardSnapshot(sequence, false, null));
        }

        string? text = null;
        if (NativeMethods.OpenClipboard(IntPtr.Zero))
        {
            try
            {
                var handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
                if (handle != IntPtr.Zero)
                {
                    var pointer = NativeMethods.GlobalLock(handle);
                    try
                    {
                        if (pointer != IntPtr.Zero)
                        {
                            text = Marshal.PtrToStringUni(pointer);
                        }
                    }
                    finally
                    {
                        if (pointer != IntPtr.Zero)
                        {
                            NativeMethods.GlobalUnlock(handle);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to read clipboard contents.");
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        return ValueTask.FromResult<ClipboardSnapshot?>(new ClipboardSnapshot(sequence, true, text));
    }
}

public sealed class WindowsPowerMonitor : ISystemLifecycleMonitor, IPowerStatusMonitor, IDisposable
{
    private readonly Channel<SystemLifecycleSignal> _lifecycleSignals = Channel.CreateUnbounded<SystemLifecycleSignal>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Channel<bool> _powerSignals = Channel.CreateUnbounded<bool>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public WindowsPowerMonitor()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public ValueTask<SystemLifecycleSignal> WaitForNextAsync(CancellationToken cancellationToken)
        => _lifecycleSignals.Reader.ReadAsync(cancellationToken);

    public ValueTask<PowerStatusSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        if (!NativeMethods.GetSystemPowerStatus(out var status))
        {
            return ValueTask.FromResult(new PowerStatusSnapshot(
                PowerSourceKind.Unknown,
                PowerChargingState.Unknown,
                null));
        }

        var powerSource = status.ACLineStatus switch
        {
            0 => PowerSourceKind.Battery,
            1 => PowerSourceKind.Ac,
            _ => PowerSourceKind.Unknown,
        };

        var hasBattery = (status.BatteryFlag & 128) == 0 && status.BatteryFlag != 255;
        int? batteryPercent = !hasBattery || status.BatteryLifePercent == 255
            ? null
            : status.BatteryLifePercent;

        var chargingState = ResolveChargingState(status, powerSource, batteryPercent);
        return ValueTask.FromResult(new PowerStatusSnapshot(powerSource, chargingState, batteryPercent));
    }

    public async ValueTask WaitForChangeAsync(CancellationToken cancellationToken)
    {
        await _powerSignals.Reader.ReadAsync(cancellationToken);
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                _lifecycleSignals.Writer.TryWrite(new SystemLifecycleSignal(SystemLifecycleSignalKind.Sleep));
                _powerSignals.Writer.TryWrite(true);
                break;
            case PowerModes.Resume:
                _lifecycleSignals.Writer.TryWrite(new SystemLifecycleSignal(SystemLifecycleSignalKind.Resume));
                _powerSignals.Writer.TryWrite(true);
                break;
            case PowerModes.StatusChange:
                _powerSignals.Writer.TryWrite(true);
                break;
        }
    }

    private static PowerChargingState ResolveChargingState(
        NativeMethods.SYSTEM_POWER_STATUS status,
        PowerSourceKind powerSource,
        int? batteryPercent)
    {
        if (status.BatteryFlag == 255)
        {
            return PowerChargingState.Unknown;
        }

        if ((status.BatteryFlag & 128) != 0)
        {
            return PowerChargingState.NotPresent;
        }

        if ((status.BatteryFlag & 8) != 0)
        {
            return PowerChargingState.Charging;
        }

        if (powerSource == PowerSourceKind.Battery)
        {
            return PowerChargingState.Discharging;
        }

        if (powerSource == PowerSourceKind.Ac && batteryPercent >= 100)
        {
            return PowerChargingState.Full;
        }

        return powerSource == PowerSourceKind.Ac
            ? PowerChargingState.NotCharging
            : PowerChargingState.Unknown;
    }
}

public sealed class WindowsProcessLifecycleMonitor : IProcessLifecycleMonitor, IDisposable
{
    private static readonly TimeSpan FallbackPollingInterval = TimeSpan.FromSeconds(2);

    private readonly Channel<ProcessLifecycleNotification> _events = Channel.CreateUnbounded<ProcessLifecycleNotification>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ILogger<WindowsProcessLifecycleMonitor> _logger;
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private readonly Queue<ProcessLifecycleNotification> _fallbackEvents = new();
    private Dictionary<int, PolledProcessSnapshot> _polledProcesses = new();
    private readonly bool _usePollingFallback;

    public WindowsProcessLifecycleMonitor(ILogger<WindowsProcessLifecycleMonitor> logger)
    {
        _logger = logger;
        ManagementEventWatcher? startWatcher = null;
        ManagementEventWatcher? stopWatcher = null;

        try
        {
            startWatcher = CreateWatcher(
                "SELECT * FROM Win32_ProcessStartTrace",
                HandleStartEvent);
            stopWatcher = CreateWatcher(
                "SELECT * FROM Win32_ProcessStopTrace",
                HandleStopEvent);

            startWatcher.Start();
            stopWatcher.Start();

            _startWatcher = startWatcher;
            _stopWatcher = stopWatcher;
        }
        catch (Exception ex)
        {
            DisposeWatcher(startWatcher, HandleStartEvent);
            DisposeWatcher(stopWatcher, HandleStopEvent);
            _usePollingFallback = true;
            _polledProcesses = CaptureProcessSnapshot();
            SafeLogWarning(
                ex,
                "WMI process lifecycle monitoring is unavailable. Falling back to process snapshot polling.");
        }
    }

    public async ValueTask<ProcessLifecycleNotification> WaitForNextAsync(CancellationToken cancellationToken)
    {
        if (!_usePollingFallback)
        {
            return await _events.Reader.ReadAsync(cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_fallbackEvents.Count > 0)
            {
                return _fallbackEvents.Dequeue();
            }

            await Task.Delay(FallbackPollingInterval, cancellationToken);
            var current = CaptureProcessSnapshot();
            EnqueueFallbackDiffs(_polledProcesses, current);
            _polledProcesses = current;
        }

        throw new OperationCanceledException(cancellationToken);
    }

    public void Dispose()
    {
        DisposeWatcher(_startWatcher, HandleStartEvent);
        DisposeWatcher(_stopWatcher, HandleStopEvent);
        _startWatcher = null;
        _stopWatcher = null;
    }

    private ManagementEventWatcher CreateWatcher(
        string query,
        EventArrivedEventHandler handler)
    {
        var watcher = new ManagementEventWatcher(new WqlEventQuery(query));
        watcher.EventArrived += handler;
        return watcher;
    }

    private void HandleStartEvent(object sender, EventArrivedEventArgs eventArgs)
    {
        try
        {
            var pid = ReadInt(eventArgs.NewEvent, "ProcessID");
            var parentPid = ReadNullableInt(eventArgs.NewEvent, "ParentProcessID");
            var processName = CollectorHelpers.NormalizeProcessName(ReadString(eventArgs.NewEvent, "ProcessName"));
            var executablePath = TryGetExecutablePath(pid);

            _events.Writer.TryWrite(new ProcessLifecycleNotification(
                ProcessLifecycleEventKind.Started,
                pid,
                processName,
                parentPid,
                executablePath,
                null));
        }
        catch (Exception ex)
        {
            SafeLogDebug(ex, "Failed to process a process start event.");
        }
    }

    private void HandleStopEvent(object sender, EventArrivedEventArgs eventArgs)
    {
        try
        {
            var pid = ReadInt(eventArgs.NewEvent, "ProcessID");
            var processName = CollectorHelpers.NormalizeProcessName(ReadString(eventArgs.NewEvent, "ProcessName"));
            var exitCode = ReadNullableInt(eventArgs.NewEvent, "ExitStatus");

            _events.Writer.TryWrite(new ProcessLifecycleNotification(
                ProcessLifecycleEventKind.Exited,
                pid,
                processName,
                null,
                null,
                exitCode));
        }
        catch (Exception ex)
        {
            SafeLogDebug(ex, "Failed to process a process stop event.");
        }
    }

    private void SafeLogWarning(Exception exception, string message)
    {
        try
        {
            _logger.LogWarning(exception, message);
        }
        catch
        {
        }
    }

    private void SafeLogDebug(Exception exception, string message)
    {
        try
        {
            _logger.LogDebug(exception, message);
        }
        catch
        {
        }
    }

    private void DisposeWatcher(ManagementEventWatcher? watcher, EventArrivedEventHandler handler)
    {
        if (watcher is null)
        {
            return;
        }

        try
        {
            watcher.EventArrived -= handler;
        }
        catch
        {
        }

        try
        {
            watcher.Stop();
        }
        catch
        {
        }

        try
        {
            watcher.Dispose();
        }
        catch
        {
        }
    }

    private static int ReadInt(ManagementBaseObject? managementEvent, string propertyName)
        => ReadNullableInt(managementEvent, propertyName) ?? 0;

    private static int? ReadNullableInt(ManagementBaseObject? managementEvent, string propertyName)
    {
        var value = managementEvent?.Properties[propertyName]?.Value;
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            int number => number,
            uint number => checked((int)number),
            ushort number => number,
            byte number => number,
            long number => checked((int)number),
            ulong number => checked((int)number),
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => null,
        };
    }

    private static string? ReadString(ManagementBaseObject? managementEvent, string propertyName)
        => managementEvent?.Properties[propertyName]?.Value?.ToString();

    private static string? TryGetExecutablePath(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return TryGetExecutablePath(process);
        }
        catch
        {
            return null;
        }
    }

    private void EnqueueFallbackDiffs(
        IReadOnlyDictionary<int, PolledProcessSnapshot> previous,
        IReadOnlyDictionary<int, PolledProcessSnapshot> current)
    {
        foreach (var started in current
                     .Where(x => !previous.ContainsKey(x.Key))
                     .OrderBy(x => x.Key))
        {
            _fallbackEvents.Enqueue(new ProcessLifecycleNotification(
                ProcessLifecycleEventKind.Started,
                started.Key,
                started.Value.ProcessName,
                started.Value.ParentProcessId,
                started.Value.ExecutablePath,
                null));
        }

        foreach (var exited in previous
                     .Where(x => !current.ContainsKey(x.Key))
                     .OrderBy(x => x.Key))
        {
            _fallbackEvents.Enqueue(new ProcessLifecycleNotification(
                ProcessLifecycleEventKind.Exited,
                exited.Key,
                exited.Value.ProcessName,
                exited.Value.ParentProcessId,
                exited.Value.ExecutablePath,
                null));
        }
    }

    private static Dictionary<int, PolledProcessSnapshot> CaptureProcessSnapshot()
    {
        var snapshot = new Dictionary<int, PolledProcessSnapshot>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var processName = CollectorHelpers.NormalizeProcessName(process.ProcessName);
                    if (string.IsNullOrWhiteSpace(processName))
                    {
                        processName = process.Id.ToString();
                    }

                    snapshot[process.Id] = new PolledProcessSnapshot(
                        processName,
                        ParentProcessId: null,
                        ExecutablePath: TryGetExecutablePath(process));
                }
                catch
                {
                }
            }
        }

        return snapshot;
    }

    private static string? TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private sealed record PolledProcessSnapshot(
        string ProcessName,
        int? ParentProcessId,
        string? ExecutablePath);
}

public sealed class WindowsNetworkConnectivityMonitor : INetworkConnectivityMonitor, IDisposable
{
    private readonly Channel<bool> _signals = Channel.CreateUnbounded<bool>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public WindowsNetworkConnectivityMonitor()
    {
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    public ValueTask<NetworkConnectivitySnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var active = GetActiveInterface();
        var isOnline = NetworkInterface.GetIsNetworkAvailable() && active is not null;

        return ValueTask.FromResult(new NetworkConnectivitySnapshot(
            isOnline,
            active?.Id,
            active?.Name,
            active?.NetworkInterfaceType.ToString()));
    }

    public async ValueTask WaitForChangeAsync(CancellationToken cancellationToken)
    {
        await _signals.Reader.ReadAsync(cancellationToken);
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
    }

    private void OnNetworkChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        _signals.Writer.TryWrite(true);
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        _signals.Writer.TryWrite(true);
    }

    private static NetworkInterface? GetActiveInterface()
        => NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up)
            .Where(x => x.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .OrderByDescending(HasGatewayAddress)
            .ThenByDescending(IsPreferredInterfaceType)
            .ThenByDescending(x => x.Speed)
            .FirstOrDefault();

    private static bool HasGatewayAddress(NetworkInterface networkInterface)
    {
        try
        {
            return networkInterface
                .GetIPProperties()
                .GatewayAddresses
                .Any(x => !x.Address.Equals(IPAddress.Any) && !x.Address.Equals(IPAddress.IPv6Any));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPreferredInterfaceType(NetworkInterface networkInterface)
        => networkInterface.NetworkInterfaceType is NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.GigabitEthernet or
            NetworkInterfaceType.Wireless80211;
}
