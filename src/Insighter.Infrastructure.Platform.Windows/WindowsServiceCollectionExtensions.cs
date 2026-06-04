using Insighter.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Insighter.Infrastructure.Platform.Windows;

public static class WindowsServiceCollectionExtensions
{
    public static IServiceCollection AddInsighterWindowsPlatform(this IServiceCollection services)
    {
        services.TryAddSingleton<IPlatformCapabilityProvider, WindowsPlatformCapabilityProvider>();
        services.TryAddSingleton<WindowsPowerMonitor>();
        services.TryAddSingleton<ISystemLifecycleMonitor>(sp => sp.GetRequiredService<WindowsPowerMonitor>());
        services.TryAddSingleton<IPowerStatusMonitor>(sp => sp.GetRequiredService<WindowsPowerMonitor>());
        services.TryAddSingleton<IForegroundWindowMonitor, WindowsForegroundWindowMonitor>();
        services.TryAddSingleton<IIdleMonitor, WindowsIdleMonitor>();
        services.TryAddSingleton<ISessionStateMonitor, WindowsSessionStateMonitor>();
        services.TryAddSingleton<IClipboardMonitor, WindowsClipboardMonitor>();
        services.TryAddSingleton<INetworkConnectivityMonitor, WindowsNetworkConnectivityMonitor>();

        services.AddSingleton<ICollector, SystemLifecycleCollector>();
        services.AddSingleton<ICollector, SessionStateCollector>();
        services.AddSingleton<ICollector, ForegroundWindowCollector>();
        services.AddSingleton<ICollector, IdleCollector>();
        services.AddSingleton<ICollector, ClipboardCollector>();
        services.AddSingleton<ICollector, PowerCollector>();
        services.AddSingleton<ICollector, NetworkConnectivityCollector>();
        return services;
    }
}
