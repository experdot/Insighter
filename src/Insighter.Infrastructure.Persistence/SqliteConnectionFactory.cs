using Microsoft.Data.Sqlite;

namespace Insighter.Infrastructure.Persistence;

public interface ISqliteConnectionFactory
{
    SqliteConnection CreateConnection();
}

public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);
}
