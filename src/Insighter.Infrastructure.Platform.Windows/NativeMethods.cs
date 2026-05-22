using System.Runtime.InteropServices;
using System.Text;

namespace Insighter.Infrastructure.Platform.Windows;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("user32.dll")]
    internal static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [DllImport("user32.dll")]
    internal static extern uint GetClipboardSequenceNumber();

    internal const uint CF_UNICODETEXT = 13;
}
