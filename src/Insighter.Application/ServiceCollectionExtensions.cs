using System.Threading.Channels;
using Insighter.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Insighter.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInsighterApplication(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IMachineContextProvider, DefaultMachineContextProvider>();
        services.TryAddSingleton<IUserSessionContextProvider, DefaultUserSessionContextProvider>();
        services.TryAddSingleton<IContentValidator, JsonContentValidator>();
        services.TryAddSingleton<IEventSink, ChannelEventSink>();
        services.TryAddSingleton<ICollectorManager, CollectorManager>();
        services.TryAddSingleton<ISettingsService, SettingsService>();
        services.TryAddSingleton<ILogQueryService, LogQueryService>();
        services.TryAddSingleton<ILogAggregationService, LogAggregationService>();
        services.TryAddSingleton<ILogRetentionService, LogRetentionService>();
        services.TryAddSingleton<IAiLogQueryService, AiLogQueryService>();
        services.TryAddSingleton<IRedactionService, DefaultRedactionService>();
        services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<InsighterOptions>>().Value;
            return Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(options.QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            });
        });
        services.AddHostedService<EventPipelineHostedService>();
        services.AddHostedService<CollectorManagerHostedService>();
        services.AddHostedService<RetentionCleanupHostedService>();
        return services;
    }
}
