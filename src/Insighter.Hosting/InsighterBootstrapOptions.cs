using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Insighter.Hosting;

public sealed class InsighterBootstrapOptions
{
    public string[] Args { get; init; } = [];

    public string? ContentRootPath { get; init; }

    public string? WebRootPath { get; init; }

    public string? DatabasePath { get; init; }

    public string? ApiUrls { get; init; }

    public Action<WebApplicationBuilder>? ConfigureBuilder { get; init; }

    public Action<WebApplication>? ConfigureApp { get; init; }
}
