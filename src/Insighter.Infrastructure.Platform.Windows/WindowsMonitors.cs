using System.Diagnostics;
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
