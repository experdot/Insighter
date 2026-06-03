using System.Net;
using System.Net.Sockets;

namespace Insighter.Agent.Tests;

public sealed class AgentTests
{
    [Fact]
    public void ActivationInfo_OpensUi_ByDefault()
    {
        var activationInfo = new AgentActivationInfo();

        var shouldOpen = activationInfo.ShouldOpenUiOnLaunch([]);

        Assert.True(shouldOpen);
    }

    [Fact]
    public void ActivationInfo_Keeps_Background_Start_Silent()
    {
        var activationInfo = new AgentActivationInfo();

        var shouldOpen = activationInfo.ShouldOpenUiOnLaunch(["--background"]);

        Assert.False(shouldOpen);
    }

    [Fact]
    public void ActivationInfo_OpenUi_Overrides_Background()
    {
        var activationInfo = new AgentActivationInfo();

        var shouldOpen = activationInfo.ShouldOpenUiOnLaunch(["--background", "--open-ui"]);

        Assert.True(shouldOpen);
    }

    [Fact]
    public void PortResolver_Avoids_Already_Bound_Port()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 5078);
        listener.Start();

        var resolver = new AgentPortResolver();

        var url = resolver.ResolveApiBaseUrl();

        Assert.Equal("http://127.0.0.1:5079", url);
    }

    [Fact]
    public async Task RuntimeStateStore_RoundTrips_State_File()
    {
        var originalOverride = Environment.GetEnvironmentVariable(AgentPaths.RootOverrideEnvironmentVariableName);
        var tempRoot = Path.Combine(Path.GetTempPath(), "Insighter.Agent.Tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(AgentPaths.RootOverrideEnvironmentVariableName, tempRoot);

        try
        {
            var store = new AgentRuntimeStateStore();
            var statePath = AgentPaths.RuntimeStatePath;
            var expected = new AgentRuntimeState
            {
                ApiBaseUrl = "http://127.0.0.1:5083",
                ProcessId = 1234,
                UpdatedAtUtc = DateTimeOffset.Parse("2026-06-03T12:00:00Z"),
            };

            await store.WriteAsync(expected, CancellationToken.None);
            var actual = await store.ReadAsync(CancellationToken.None);

            Assert.NotNull(actual);
            Assert.Equal(expected.ApiBaseUrl, actual!.ApiBaseUrl);
            Assert.Equal(expected.ProcessId, actual.ProcessId);
            Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);

            store.Delete();
            Assert.False(File.Exists(statePath));
        }
        finally
        {
            Environment.SetEnvironmentVariable(AgentPaths.RootOverrideEnvironmentVariableName, originalOverride);

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
