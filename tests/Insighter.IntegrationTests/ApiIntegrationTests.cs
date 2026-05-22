using System.Net;
using System.Net.Http.Json;
using Dapper;
using Insighter.Contracts;
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
        var result = await client.PutAsJsonAsync("/api/settings", new SettingsUpdateRequest(10, "MetadataOnly", 20, 30, ["code"]));

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var payload = await result.Content.ReadFromJsonAsync<SettingsResponse>();
        Assert.NotNull(payload);
        Assert.Equal("MetadataOnly", payload!.DefaultClipboardMode);
        Assert.Equal(10, payload.RetentionDays);
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
                Id = id,
                OccurredAtUtc = occurredAtUtc.ToUnixTimeMilliseconds(),
                EventType = eventType,
                CollectorId = collectorId,
                Severity = severity,
                UserSessionId = userSessionId,
                MachineId = machineId,
                Content = content,
            });
    }
}
