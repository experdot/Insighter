using Insighter.Application;
using Insighter.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Insighter.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddInsighterPersistence(this IServiceCollection services, string databasePath)
    {
        services.TryAddSingleton<ISqliteConnectionFactory>(_ => new SqliteConnectionFactory(databasePath));
        services.TryAddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.TryAddSingleton<ILogEventRepository, SqliteLogEventRepository>();
        services.TryAddSingleton<ICollectorConfigRepository, SqliteCollectorConfigRepository>();
        services.TryAddSingleton<ICollectorStateRepository, SqliteCollectorStateRepository>();
        services.TryAddSingleton<IAppSettingsRepository, SqliteAppSettingsRepository>();
        return services;
    }
}
