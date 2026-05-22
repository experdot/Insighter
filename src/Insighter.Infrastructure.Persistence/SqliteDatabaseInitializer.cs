using System.Reflection;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Insighter.Infrastructure.Persistence;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteDatabaseInitializer> _logger;

    public SqliteDatabaseInitializer(ISqliteConnectionFactory connectionFactory, ILogger<SqliteDatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var scripts = Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var scriptName in scripts)
        {
            var version = ParseVersion(scriptName);
            var exists = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'SchemaVersions';",
                    cancellationToken: cancellationToken));

            if (exists > 0)
            {
                var applied = await connection.ExecuteScalarAsync<long?>(
                    new CommandDefinition("SELECT Version FROM SchemaVersions WHERE Version = @Version;", new { Version = version }, cancellationToken: cancellationToken));

                if (applied.HasValue)
                {
                    continue;
                }
            }

            await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(scriptName)
                ?? throw new InvalidOperationException($"Unable to load migration script '{scriptName}'.");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT OR IGNORE INTO SchemaVersions (Version, AppliedAtUtc) VALUES (@Version, @AppliedAtUtc);",
                    new { Version = version, AppliedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    transaction,
                    cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Applied migration {Version}.", version);
        }
    }

    private static int ParseVersion(string resourceName)
    {
        var name = resourceName.Split('.').Reverse().Skip(1).FirstOrDefault();
        if (name is null || name.Length < 3 || !int.TryParse(name[..3], out var version))
        {
            throw new InvalidOperationException($"Unable to parse migration version from '{resourceName}'.");
        }

        return version;
    }
}
