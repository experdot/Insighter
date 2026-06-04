using Insighter.Domain;

namespace Insighter.Infrastructure.Platform.Windows;

public sealed class WindowsPlatformCapabilityProvider : IPlatformCapabilityProvider
{
    public IReadOnlyCollection<PlatformCapability> GetCapabilities()
    {
        if (!OperatingSystem.IsWindows())
        {
            return
            [
                new PlatformCapability(CollectorIds.SystemLifecycle, CapabilitySupportLevel.Unsupported, "Only supported on Windows."),
                new PlatformCapability(CollectorIds.SessionState, CapabilitySupportLevel.Unsupported, "Only supported on Windows."),
                new PlatformCapability(CollectorIds.ForegroundWindow, CapabilitySupportLevel.Unsupported, "Only supported on Windows."),
                new PlatformCapability(CollectorIds.Idle, CapabilitySupportLevel.Unsupported, "Only supported on Windows."),
                new PlatformCapability(CollectorIds.Clipboard, CapabilitySupportLevel.Unsupported, "Only supported on Windows."),
                new PlatformCapability(CollectorIds.Power, CapabilitySupportLevel.Unsupported, "Only supported on Windows."),
                new PlatformCapability(CollectorIds.NetworkConnectivity, CapabilitySupportLevel.Unsupported, "Only supported on Windows."),
            ];
        }

        return
        [
            new PlatformCapability(CollectorIds.SystemLifecycle, CapabilitySupportLevel.PartiallySupported, "Shutdown events are best-effort in console mode."),
            new PlatformCapability(CollectorIds.SessionState, CapabilitySupportLevel.Supported, null),
            new PlatformCapability(CollectorIds.ForegroundWindow, CapabilitySupportLevel.Supported, null),
            new PlatformCapability(CollectorIds.Idle, CapabilitySupportLevel.Supported, null),
            new PlatformCapability(CollectorIds.Clipboard, CapabilitySupportLevel.Supported, null),
            new PlatformCapability(CollectorIds.Power, CapabilitySupportLevel.Supported, null),
            new PlatformCapability(CollectorIds.NetworkConnectivity, CapabilitySupportLevel.Supported, null),
        ];
    }
}
