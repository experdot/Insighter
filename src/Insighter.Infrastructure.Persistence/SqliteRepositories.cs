using Dapper;
using Insighter.Domain;

namespace Insighter.Infrastructure.Persistence;

public sealed class SqliteLogEventRepository : ILogEventRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteLogEventRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InsertBatchAsync(IReadOnlyCollection<LogEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            INSERT INTO LogEvents (Id, OccurredAtUtc, EventType, CollectorId, Severity, UserSessionId, MachineId, Content)
            VALUES (@Id, @OccurredAtUtc, @EventType, @CollectorId, @Severity, @UserSessionId, @MachineId, @Content);
            """;

        var rows = events.Select(ToRow).ToArray();
        await connection.ExecuteAsync(new CommandDefinition(sql, rows, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PagedResult<LogEvent>> SearchAsync(LogQueryFilter filter, CancellationToken cancellationToken)
    {
        var where = new List<string> { "1 = 1" };
        var parameters = new DynamicParameters();

        AddSearchParameters(filter, where, parameters);

        var offset = (filter.Page - 1) * filter.PageSize;
        parameters.Add("@Limit", filter.PageSize);
        parameters.Add("@Offset", offset);

        var countSql = $"SELECT COUNT(1) FROM LogEvents WHERE {string.Join(" AND ", where)};";
        var dataSql = $"""
            SELECT Id, OccurredAtUtc, EventType, CollectorId, Severity, UserSessionId, MachineId, Content
            FROM LogEvents
            WHERE {string.Join(" AND ", where)}
            ORDER BY OccurredAtUtc DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<LogEventRow>(new CommandDefinition(dataSql, parameters, cancellationToken: cancellationToken));
        return new PagedResult<LogEvent>(rows.Select(ToDomain).ToArray(), filter.Page, filter.PageSize, total);
    }

    public async Task<LogEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<LogEventRow>(
            new CommandDefinition(
                """
                SELECT Id, OccurredAtUtc, EventType, CollectorId, Severity, UserSessionId, MachineId, Content
                FROM LogEvents
                WHERE Id = @Id;
                """,
                new { Id = id.ToString() },
                cancellationToken: cancellationToken));

        return row is null ? null : ToDomain(row);
    }

    public async Task<IReadOnlyList<LogEvent>> GetRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<LogEventRow>(
            new CommandDefinition(
                """
                SELECT Id, OccurredAtUtc, EventType, CollectorId, Severity, UserSessionId, MachineId, Content
                FROM LogEvents
                WHERE OccurredAtUtc >= @FromUtc AND OccurredAtUtc <= @ToUtc
                ORDER BY OccurredAtUtc ASC;
                """,
                new
                {
                    FromUtc = fromUtc.ToUnixTimeMilliseconds(),
                    ToUtc = toUtc.ToUnixTimeMilliseconds(),
                },
                cancellationToken: cancellationToken));

        return rows.Select(ToDomain).ToArray();
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset thresholdUtc, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM LogEvents WHERE OccurredAtUtc < @ThresholdUtc;",
                new { ThresholdUtc = thresholdUtc.ToUnixTimeMilliseconds() },
                cancellationToken: cancellationToken));
    }

    private static void AddSearchParameters(LogQueryFilter filter, ICollection<string> where, DynamicParameters parameters)
    {
        if (filter.FromUtc.HasValue)
        {
            where.Add("OccurredAtUtc >= @FromUtc");
            parameters.Add("@FromUtc", filter.FromUtc.Value.ToUnixTimeMilliseconds());
        }

        if (filter.ToUtc.HasValue)
        {
            where.Add("OccurredAtUtc <= @ToUtc");
            parameters.Add("@ToUtc", filter.ToUtc.Value.ToUnixTimeMilliseconds());
        }

        if (!string.IsNullOrWhiteSpace(filter.EventType))
        {
            where.Add("EventType = @EventType");
            parameters.Add("@EventType", filter.EventType);
        }

        if (!string.IsNullOrWhiteSpace(filter.CollectorId))
        {
            where.Add("CollectorId = @CollectorId");
            parameters.Add("@CollectorId", filter.CollectorId);
        }

        if (filter.Severity.HasValue)
        {
            where.Add("Severity = @Severity");
            parameters.Add("@Severity", (int)filter.Severity.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.UserSessionId))
        {
            where.Add("UserSessionId = @UserSessionId");
            parameters.Add("@UserSessionId", filter.UserSessionId);
        }

        if (!string.IsNullOrWhiteSpace(filter.MachineId))
        {
            where.Add("MachineId = @MachineId");
            parameters.Add("@MachineId", filter.MachineId);
        }
    }

    private static LogEventRow ToRow(LogEvent logEvent) => new()
    {
        Id = logEvent.Id.ToString(),
        OccurredAtUtc = logEvent.OccurredAtUtc.ToUnixTimeMilliseconds(),
        EventType = logEvent.EventType,
        CollectorId = logEvent.CollectorId,
        Severity = (int)logEvent.Severity,
        UserSessionId = logEvent.UserSessionId,
        MachineId = logEvent.MachineId,
        Content = logEvent.Content,
    };

    private static LogEvent ToDomain(LogEventRow row)
        => new(
            Guid.Parse(row.Id),
            DateTimeOffset.FromUnixTimeMilliseconds(row.OccurredAtUtc),
            row.EventType,
            row.CollectorId,
            (EventSeverity)row.Severity,
            row.UserSessionId,
            row.MachineId,
            row.Content);
}

public sealed class SqliteCollectorConfigRepository : ICollectorConfigRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteCollectorConfigRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CollectorConfig>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<CollectorConfigRow>(
            new CommandDefinition("SELECT CollectorId, Enabled, Version, UpdatedAtUtc, SettingsJson FROM CollectorConfigs;", cancellationToken: cancellationToken));
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<CollectorConfig?> GetAsync(string collectorId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<CollectorConfigRow>(
            new CommandDefinition(
                "SELECT CollectorId, Enabled, Version, UpdatedAtUtc, SettingsJson FROM CollectorConfigs WHERE CollectorId = @CollectorId;",
                new { CollectorId = collectorId },
                cancellationToken: cancellationToken));
        return row is null ? null : ToDomain(row);
    }

    public async Task UpsertAsync(CollectorConfig config, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO CollectorConfigs (CollectorId, Enabled, Version, UpdatedAtUtc, SettingsJson)
                VALUES (@CollectorId, @Enabled, @Version, @UpdatedAtUtc, @SettingsJson)
                ON CONFLICT(CollectorId) DO UPDATE SET
                    Enabled = excluded.Enabled,
                    Version = excluded.Version,
                    UpdatedAtUtc = excluded.UpdatedAtUtc,
                    SettingsJson = excluded.SettingsJson;
                """,
                new
                {
                    config.CollectorId,
                    Enabled = config.Enabled ? 1 : 0,
                    config.Version,
                    UpdatedAtUtc = config.UpdatedAtUtc.ToUnixTimeMilliseconds(),
                    config.SettingsJson,
                },
                cancellationToken: cancellationToken));
    }

    private static CollectorConfig ToDomain(CollectorConfigRow row)
        => new(
            row.CollectorId,
            row.Enabled != 0,
            row.Version,
            DateTimeOffset.FromUnixTimeMilliseconds(row.UpdatedAtUtc),
            row.SettingsJson);
}

public sealed class SqliteCollectorStateRepository : ICollectorStateRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteCollectorStateRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CollectorState>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<CollectorStateRow>(
            new CommandDefinition("SELECT CollectorId, Status, LastHeartbeatUtc, LastError, UpdatedAtUtc FROM CollectorStates;", cancellationToken: cancellationToken));
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<CollectorState?> GetAsync(string collectorId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<CollectorStateRow>(
            new CommandDefinition(
                "SELECT CollectorId, Status, LastHeartbeatUtc, LastError, UpdatedAtUtc FROM CollectorStates WHERE CollectorId = @CollectorId;",
                new { CollectorId = collectorId },
                cancellationToken: cancellationToken));
        return row is null ? null : ToDomain(row);
    }

    public async Task UpsertAsync(CollectorState state, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO CollectorStates (CollectorId, Status, LastHeartbeatUtc, LastError, UpdatedAtUtc)
                VALUES (@CollectorId, @Status, @LastHeartbeatUtc, @LastError, @UpdatedAtUtc)
                ON CONFLICT(CollectorId) DO UPDATE SET
                    Status = excluded.Status,
                    LastHeartbeatUtc = excluded.LastHeartbeatUtc,
                    LastError = excluded.LastError,
                    UpdatedAtUtc = excluded.UpdatedAtUtc;
                """,
                new
                {
                    state.CollectorId,
                    Status = (int)state.Status,
                    LastHeartbeatUtc = state.LastHeartbeatUtc?.ToUnixTimeMilliseconds(),
                    state.LastError,
                    UpdatedAtUtc = state.UpdatedAtUtc.ToUnixTimeMilliseconds(),
                },
                cancellationToken: cancellationToken));
    }

    private static CollectorState ToDomain(CollectorStateRow row)
        => new(
            row.CollectorId,
            (CollectorStatus)row.Status,
            row.LastHeartbeatUtc.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.LastHeartbeatUtc.Value) : null,
            row.LastError,
            DateTimeOffset.FromUnixTimeMilliseconds(row.UpdatedAtUtc));
}

public sealed class SqliteAppSettingsRepository : IAppSettingsRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteAppSettingsRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<AppSettingsRow>(
            new CommandDefinition("SELECT Key, Value FROM AppSettings;", cancellationToken: cancellationToken));
        return rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    public Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
        => UpsertManyAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [key] = value }, cancellationToken);

    public async Task UpsertManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        if (values.Count == 0)
        {
            return;
        }

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            INSERT INTO AppSettings (Key, Value, UpdatedAtUtc)
            VALUES (@Key, @Value, @UpdatedAtUtc)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var rows = values.Select(kvp => new { Key = kvp.Key, Value = kvp.Value, UpdatedAtUtc = now }).ToArray();
        await connection.ExecuteAsync(new CommandDefinition(sql, rows, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }
}
