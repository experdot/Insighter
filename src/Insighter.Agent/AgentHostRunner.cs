using Insighter.Hosting;
using Microsoft.AspNetCore.Builder;

namespace Insighter.Agent;

internal interface IAgentHostRunner : IAsyncDisposable
{
    string ApiBaseUrl { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

internal sealed class AgentHostRunner : IAgentHostRunner
{
    private readonly IAgentPortResolver _portResolver;
    private readonly IAgentRuntimeStateStore _runtimeStateStore;
    private WebApplication? _app;

    public AgentHostRunner(IAgentPortResolver portResolver, IAgentRuntimeStateStore runtimeStateStore)
    {
        _portResolver = portResolver;
        _runtimeStateStore = runtimeStateStore;
        ApiBaseUrl = _portResolver.ResolveApiBaseUrl();
    }

    public string ApiBaseUrl { get; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
        {
            return;
        }

        AgentPaths.EnsureDirectories();

        _app = await InsighterBootstrap.BuildAsync(new InsighterBootstrapOptions
        {
            Args = [],
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            DatabasePath = AgentPaths.DatabasePath,
            ApiUrls = ApiBaseUrl,
        }, cancellationToken);

        await _app.StartAsync(cancellationToken);
        await _runtimeStateStore.WriteAsync(new AgentRuntimeState
        {
            ApiBaseUrl = ApiBaseUrl,
            ProcessId = Environment.ProcessId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is null)
        {
            return;
        }

        try
        {
            await _app.StopAsync(cancellationToken);
        }
        finally
        {
            await _app.DisposeAsync();
            _app = null;
            _runtimeStateStore.Delete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
