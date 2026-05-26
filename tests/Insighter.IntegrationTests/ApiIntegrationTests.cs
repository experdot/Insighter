using System.Net;
using System.Net.Http.Json;
using Dapper;
using Insighter.Application;
using Insighter.Contracts;
using Insighter.Domain;
using Insighter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Insighter.IntegrationTests;

public sealed class ApiIntegrationTests : IClassFixture<InsighterWebApplicationFactory>
{
    private readonly InsighterWebApplicationFactory _factory;

    public ApiIntegrationTests(InsighterWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCollectors_Returns_DefaultCollectors()
    {
        var client = _factory.CreateClient();
        var collectors = await client.GetFromJsonAsync<CollectorResponse[]>("/api/collectors");

        Assert.NotNull(collectors);
        Assert.Equal(8, collectors!.Length);
        Assert.Contains(collectors, x => x.CollectorId == "ProcessLifecycleCollector");
        Assert.Contains(collectors, x => x.CollectorId == "PowerCollector");
        Assert.Contains(collectors, x => x.CollectorId == "NetworkConnectivityCollector");
    }

    [Fact]
    public async Task GetLogs_Returns_PagedResponse()
    {
        await _factory.InsertLogAsync(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Process.Started",
            "ProcessLifecycleCollector",
            0,
            "1",
            Environment.MachineName,
            """{"schemaVersion":1,"platform":"windows","data":{"processId":1,"processName":"test","executablePath":"C:\\test.exe","executablePathHash":"ABC"}}""");

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/logs?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<LogEventResponse>>();
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount >= 1);
        Assert.Contains(payload.Items, x => x.EventType == "Process.Started");
    }

    [Fact]
    public async Task UpdateSettings_Persists_And_Returns_Value()
    {
        var client = _factory.CreateClient();
        var result = await client.PutAsJsonAsync("/api/settings", new SettingsUpdateRequest(10));

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var payload = await result.Content.ReadFromJsonAsync<SettingsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(10, payload.RetentionDays);
    }

    [Fact]
    public async Task RetentionCleanup_Deletes_Expired_LogEvents()
    {
        var oldId = Guid.NewGuid();
        var recentId = Guid.NewGuid();
        await _factory.SetAppSettingAsync(SettingsKeys.RetentionDays, "3");
        await _factory.InsertLogAsync(
            oldId,
            DateTimeOffset.UtcNow.AddDays(-10),
            "System.Startup",
            "SystemLifecycleCollector",
            0,
            "1",
            Environment.MachineName,
            """{"schemaVersion":1,"platform":"windows","data":{"source":"old"}}""");
        await _factory.InsertLogAsync(
            recentId,
            DateTimeOffset.UtcNow.AddDays(-1),
            "System.Startup",
            "SystemLifecycleCollector",
            0,
            "1",
            Environment.MachineName,
            """{"schemaVersion":1,"platform":"windows","data":{"source":"recent"}}""");

        using var scope = _factory.Services.CreateScope();
        var retentionService = scope.ServiceProvider.GetRequiredService<ILogRetentionService>();

        var deleted = await retentionService.DeleteExpiredAsync(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.False(await _factory.LogExistsAsync(oldId));
        Assert.True(await _factory.LogExistsAsync(recentId));
    }
}

public sealed class InsighterWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), "Insighter.Tests", $"{Guid.NewGuid():N}.db");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        Environment.SetEnvironmentVariable("Insighter__DatabasePath", _databasePath);

        using var scope = Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeAsync(CancellationToken.None);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("Insighter__DatabasePath", null);
        await base.DisposeAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }

                break;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(100);
            }
            catch (IOException)
            {
                break;
            }
        }
    }

    public async Task InsertLogAsync(Guid id, DateTimeOffset occurredAtUtc, string eventType, string collectorId, int severity, string userSessionId, string machineId, string content)
    {
        using var scope = Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqliteConnectionFactory>();
        using var connection = factory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO LogEvents (Id, OccurredAtUtc, EventType, CollectorId, Severity, UserSessionId, MachineId, Content)
            VALUES (@Id, @OccurredAtUtc, @EventType, @CollectorId, @Severity, @UserSessionId, @MachineId, @Content);
            """,
            new
            {
                Id = id.ToString(),
                OccurredAtUtc = occurredAtUtc.ToUnixTimeMilliseconds(),
                EventType = eventType,
                CollectorId = collectorId,
                Severity = severity,
                UserSessionId = userSessionId,
                MachineId = machineId,
                Content = content,
            });
    }

    public async Task SetAppSettingAsync(string key, string value)
    {
        using var scope = Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAppSettingsRepository>();
        await repository.UpsertAsync(key, value, CancellationToken.None);
    }

    public async Task<bool> LogExistsAsync(Guid id)
    {
        using var scope = Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqliteConnectionFactory>();
        using var connection = factory.CreateConnection();
        await connection.OpenAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM LogEvents WHERE Id = @Id;",
            new { Id = id.ToString() });
        return count > 0;
    }
}
