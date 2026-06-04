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
        Assert.Equal(7, collectors!.Length);
        Assert.Contains(collectors, x => x.CollectorId == "PowerCollector");
        Assert.Contains(collectors, x => x.CollectorId == "NetworkConnectivityCollector");
    }

    [Fact]
    public async Task GetLogs_Returns_PagedResponse()
    {
        await _factory.InsertLogAsync(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Window.Activated",
            "ForegroundWindowCollector",
            0,
            "1",
            Environment.MachineName,
            """{"schemaVersion":1,"platform":"windows","data":{"processId":1,"processName":"test","windowTitle":"Editor"}}""");

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/logs?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<LogEventResponse>>();
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount >= 1);
        Assert.Contains(payload.Items, x => x.EventType == "Window.Activated");
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

    [Fact]
    public async Task GraphQlAiQuery_Returns_Filtered_Rows()
    {
        await _factory.InsertLogAsync(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-5),
            "Window.Activated",
            "ForegroundWindowCollector",
            0,
            "1",
            Environment.MachineName,
            """{"schemaVersion":1,"platform":"windows","data":{"processName":"Code","windowTitle":"Repo"}}""");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/graphql",
            new
            {
                query = """
                    {
                      aiQuery(input: {
                        filters: [
                          { field: "data.processName", operator: "contains", value: "code" }
                        ]
                      }) {
                        mode
                        meta {
                          totalMatched
                          returnedCount
                        }
                        items {
                          eventType
                          collectorId
                          platform
                          fields {
                            name
                            value
                          }
                        }
                      }
                    }
                    """,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GraphQlResponse<AiQueryPayload>>();
        Assert.NotNull(payload);
        Assert.Null(payload!.Errors);
        Assert.NotNull(payload.Data);
        Assert.Equal("ROWS", payload.Data!.AiQuery.Mode);
        Assert.True(payload.Data.AiQuery.Meta.TotalMatched >= 1);
        Assert.Contains(payload.Data.AiQuery.Items, x => x.Fields.Any(field => field.Name == "data.processName" && field.Value == "Code"));
    }

    [Fact]
    public async Task GraphQlAiQuery_Returns_Context()
    {
        var anchorId = Guid.NewGuid();
        await _factory.InsertLogAsync(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-3),
            "System.Startup",
            "SystemLifecycleCollector",
            0,
            "1",
            Environment.MachineName,
            """{"schemaVersion":1,"platform":"windows","data":{"source":"start"}}""");
        await _factory.InsertLogAsync(
            anchorId,
            DateTimeOffset.UtcNow.AddMinutes(-2),
            "Window.Activated",
            "ForegroundWindowCollector",
            0,
            "1",
            Environment.MachineName,
            """{"schemaVersion":1,"platform":"windows","data":{"processName":"Code"}}""");
        await _factory.InsertLogAsync(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            "Clipboard.Changed",
            "ClipboardCollector",
            0,
            "1",
            Environment.MachineName,
            """{"schemaVersion":1,"platform":"windows","data":{"captureMode":"Preview"}}""");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/graphql",
            new
            {
                query = $$"""
                    {
                      aiQuery(input: {
                        context: {
                          anchorEventId: "{{anchorId}}",
                          beforeCount: 1,
                          afterCount: 1
                        }
                      }) {
                        mode
                        anchor {
                          id
                        }
                        before {
                          eventType
                        }
                        after {
                          eventType
                        }
                      }
                    }
                    """,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GraphQlResponse<AiQueryPayload>>();
        Assert.NotNull(payload);
        Assert.Null(payload!.Errors);
        Assert.Equal("CONTEXT", payload.Data!.AiQuery.Mode);
        Assert.Equal(anchorId, payload.Data.AiQuery.Anchor!.Id);
        Assert.Single(payload.Data.AiQuery.Before);
        Assert.Single(payload.Data.AiQuery.After);
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

public sealed class GraphQlResponse<T>
{
    public T? Data { get; set; }

    public GraphQlError[]? Errors { get; set; }
}

public sealed class GraphQlError
{
    public string? Message { get; set; }

    public string? Code { get; set; }
}

public sealed class AiQueryPayload
{
    public AiQueryResultDto AiQuery { get; set; } = new();
}

public sealed class AiQueryResultDto
{
    public string Mode { get; set; } = string.Empty;

    public AiQueryMetaDto Meta { get; set; } = new();

    public AiQueryEventRowDto[] Items { get; set; } = [];

    public AiQueryGroupRowDto[] Groups { get; set; } = [];

    public AiQueryEventRowDto? Anchor { get; set; }

    public AiQueryEventRowDto[] Before { get; set; } = [];

    public AiQueryEventRowDto[] After { get; set; } = [];
}

public sealed class AiQueryMetaDto
{
    public int TotalMatched { get; set; }

    public int ReturnedCount { get; set; }
}

public sealed class AiQueryEventRowDto
{
    public Guid Id { get; set; }

    public string? EventType { get; set; }

    public string? CollectorId { get; set; }

    public string? Platform { get; set; }

    public AiQueryFieldDto[] Fields { get; set; } = [];
}

public sealed class AiQueryFieldDto
{
    public string? Name { get; set; }

    public string? Value { get; set; }
}

public sealed class AiQueryGroupRowDto
{
    public int Count { get; set; }

    public AiQueryFieldDto[] Key { get; set; } = [];
}
