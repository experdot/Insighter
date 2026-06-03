using Insighter.Hosting;

var app = await InsighterBootstrap.BuildAsync(new InsighterBootstrapOptions
{
    Args = args,
});

await app.RunAsync();

public partial class Program
{
}
