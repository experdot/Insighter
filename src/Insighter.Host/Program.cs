using System.Net;
using System.Text.Json.Nodes;
using Insighter.Application;
using Insighter.Contracts;
using Insighter.Domain;
using Insighter.Infrastructure.Persistence;
using Insighter.Infrastructure.Platform.Windows;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<InsighterOptions>(builder.Configuration.GetSection(InsighterOptions.SectionName));
builder.Services.AddRouting();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();

var startupOptions = builder.Configuration.GetSection(InsighterOptions.SectionName).Get<InsighterOptions>() ?? new InsighterOptions();
var databasePath = string.IsNullOrWhiteSpace(startupOptions.DatabasePath)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Insighter", "insighter.db")
    : startupOptions.DatabasePath;

builder.WebHost.UseUrls(startupOptions.ApiUrls);
builder.Services.AddInsighterPersistence(databasePath);
builder.Services.AddInsighterApplication();
builder.Services.AddInsighterWindowsPlatform();

var app = builder.Build();
var spaIndexPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;
        var traceId = context.TraceIdentifier;

        var response = exception switch
        {
            ApiException apiException => new ErrorResponse(apiException.Code, apiException.Message, apiException.Details, traceId),
            _ => new ErrorResponse("internal_error", "An unexpected error occurred.", null, traceId),
        };

        context.Response.StatusCode = exception is ApiException api ? (int)api.StatusCode : (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    });
});

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await initializer.InitializeAsync(CancellationToken.None);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/meta", () => Results.Ok(new { name = "Insighter", version = 1 }));

app.MapGet("/api/collectors", async (ICollectorManager manager, CancellationToken cancellationToken) =>
{
    var collectors = await manager.GetCollectorsAsync(cancellationToken);
    return Results.Ok(collectors.Select(ToCollectorResponse));
});

app.MapGet("/api/collectors/{collectorId}", async (string collectorId, ICollectorManager manager, CancellationToken cancellationToken) =>
{
    var collector = await manager.GetCollectorAsync(collectorId, cancellationToken);
    return Results.Ok(ToCollectorResponse(collector));
});

app.MapPost("/api/collectors/{collectorId}/start", async (string collectorId, ICollectorManager manager, CancellationToken cancellationToken) =>
{
    await manager.StartCollectorAsync(collectorId, cancellationToken);
    var collector = await manager.GetCollectorAsync(collectorId, cancellationToken);
    return Results.Ok(ToCollectorResponse(collector));
});

app.MapPost("/api/collectors/{collectorId}/stop", async (string collectorId, ICollectorManager manager, CancellationToken cancellationToken) =>
{
    await manager.StopCollectorAsync(collectorId, cancellationToken);
    var collector = await manager.GetCollectorAsync(collectorId, cancellationToken);
    return Results.Ok(ToCollectorResponse(collector));
});

app.MapPut("/api/collectors/{collectorId}/config", async (string collectorId, CollectorConfigRequest request, ICollectorManager manager, CancellationToken cancellationToken) =>
{
    var details = await manager.UpdateCollectorConfigAsync(
        collectorId,
        request.Enabled,
        (request.Settings ?? new JsonObject()).ToJsonString(JsonHelpers.SerializerOptions),
        cancellationToken);

    return Results.Ok(ToCollectorResponse(details));
});

app.MapGet("/api/logs", async (
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    string? eventType,
    string? collectorId,
    EventSeverity? severity,
    string? userSessionId,
    string? machineId,
    int? page,
    int? pageSize,
    ILogQueryService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.SearchAsync(
        new LogQueryFilter(
            fromUtc,
            toUtc,
            eventType,
            collectorId,
            severity,
            userSessionId,
            machineId,
            page ?? 1,
            pageSize ?? 50),
        cancellationToken);

    return Results.Ok(new PagedResponse<LogEventResponse>(
        result.Items.Select(ToLogEventResponse).ToArray(),
        result.Page,
        result.PageSize,
        result.TotalCount,
        result.TotalPages));
});

app.MapGet("/api/logs/{id:guid}", async (Guid id, ILogQueryService service, CancellationToken cancellationToken) =>
{
    var logEvent = await service.GetByIdAsync(id, cancellationToken);
    return logEvent is null ? Results.NotFound() : Results.Ok(ToLogEventResponse(logEvent));
});

app.MapGet("/api/dashboard/summary", async (DateTimeOffset? fromUtc, DateTimeOffset? toUtc, ILogAggregationService service, CancellationToken cancellationToken) =>
{
    var from = fromUtc ?? DateTimeOffset.UtcNow.AddDays(-1);
    var to = toUtc ?? DateTimeOffset.UtcNow;
    var summary = await service.GetSummaryAsync(from, to, cancellationToken);
    return Results.Ok(new DashboardSummaryResponse(
        summary.TotalEvents,
        summary.WindowActivationCount,
        summary.ClipboardChangeCount,
        summary.IdlePeriodCount,
        summary.TotalIdleDurationSeconds,
        summary.EventCountsByType));
});

app.MapGet("/api/dashboard/timeline", async (DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? bucket, ILogAggregationService service, CancellationToken cancellationToken) =>
{
    var from = fromUtc ?? DateTimeOffset.UtcNow.AddDays(-1);
    var to = toUtc ?? DateTimeOffset.UtcNow;
    var timeline = await service.GetTimelineAsync(from, to, string.IsNullOrWhiteSpace(bucket) ? "hour" : bucket, cancellationToken);
    return Results.Ok(timeline.Select(x => new TimelineBucketResponse(x.BucketStartUtc, x.BucketEndUtc, x.TotalEvents, x.EventCountsByType)));
});

app.MapGet("/api/platform/capabilities", (IPlatformCapabilityProvider provider) =>
{
    var capabilities = provider.GetCapabilities()
        .Select(x => new PlatformCapabilityResponse(x.CapabilityId, x.SupportLevel.ToString(), x.Reason));
    return Results.Ok(capabilities);
});

app.MapGet("/api/settings", async (ISettingsService service, CancellationToken cancellationToken) =>
{
    var settings = await service.GetSnapshotAsync(cancellationToken);
    return Results.Ok(ToSettingsResponse(settings));
});

app.MapPut("/api/settings", async (SettingsUpdateRequest request, ISettingsService service, CancellationToken cancellationToken) =>
{
    var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    if (request.RetentionDays.HasValue)
    {
        updates[SettingsKeys.RetentionDays] = request.RetentionDays.Value.ToString();
    }

    var settings = await service.UpdateAsync(updates, cancellationToken);
    return Results.Ok(ToSettingsResponse(settings));
});

app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        return;
    }

    if (!File.Exists(spaIndexPath))
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(
            "frontend_not_built",
            "Frontend assets are not available. Run 'pnpm build' in the web workspace.",
            null,
            context.TraceIdentifier));
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(spaIndexPath);
});

app.Run();

return;

static CollectorResponse ToCollectorResponse(CollectorDetails details)
    => new(
        details.CollectorId,
        details.DisplayName,
        details.Enabled,
        details.Status.ToString(),
        details.Capability.ToString(),
        details.CapabilityReason,
        details.LastHeartbeatUtc,
        details.LastError,
        details.ConfigVersion,
        details.Settings);

static LogEventResponse ToLogEventResponse(LogEvent logEvent)
    => new(
        logEvent.Id,
        logEvent.OccurredAtUtc,
        logEvent.EventType,
        logEvent.CollectorId,
        logEvent.Severity.ToString(),
        logEvent.UserSessionId,
        logEvent.MachineId,
        JsonNode.Parse(logEvent.Content) ?? new JsonObject());

static SettingsResponse ToSettingsResponse(AppSettingsSnapshot settings)
    => new(
        settings.DatabasePath,
        settings.ApiUrls,
        settings.QueueCapacity,
        settings.RetentionDays,
        settings.QueryMaxPageSize,
        settings.AutoStartCollectors);

public partial class Program
{
}
