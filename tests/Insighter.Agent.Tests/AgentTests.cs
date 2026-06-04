using System.Net;
using System.Net.Sockets;
using Windows.ApplicationModel;

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

    [Fact]
    public void OpenRequestStore_RoundTrips_Pending_Marker()
    {
        var originalOverride = Environment.GetEnvironmentVariable(AgentPaths.RootOverrideEnvironmentVariableName);
        var tempRoot = Path.Combine(Path.GetTempPath(), "Insighter.Agent.Tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(AgentPaths.RootOverrideEnvironmentVariableName, tempRoot);

        try
        {
            var store = new AgentOpenRequestStore();

            store.MarkPending();

            Assert.True(File.Exists(AgentPaths.PendingOpenUiPath));
            Assert.True(store.ConsumePending());
            Assert.False(File.Exists(AgentPaths.PendingOpenUiPath));
            Assert.False(store.ConsumePending());
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

    [Fact]
    public async Task StartupManager_Reports_Unavailable_When_Task_Cannot_Be_Resolved()
    {
        var manager = new AgentStartupManager(new StubStartupTaskAccessor(null));

        var state = await manager.GetStateAsync();

        Assert.False(state.IsAvailable);
        Assert.False(state.IsEnabled);
        Assert.False(state.IsPolicyManaged);
        Assert.Null(state.TaskState);
    }

    [Fact]
    public async Task StartupManager_Reports_Enabled_State_When_Task_Is_Enabled()
    {
        var manager = new AgentStartupManager(new StubStartupTaskAccessor(new StubStartupTask(StartupTaskState.Enabled)));

        var state = await manager.GetStateAsync();

        Assert.True(state.IsAvailable);
        Assert.True(state.IsEnabled);
        Assert.False(state.IsPolicyManaged);
        Assert.Equal(StartupTaskState.Enabled, state.TaskState);
    }

    [Fact]
    public async Task StartupManager_Disable_Transitions_Task_To_Disabled()
    {
        var task = new StubStartupTask(StartupTaskState.Enabled);
        var manager = new AgentStartupManager(new StubStartupTaskAccessor(task));

        var state = await manager.DisableAsync();

        Assert.True(state.IsAvailable);
        Assert.False(state.IsEnabled);
        Assert.Equal(StartupTaskState.Disabled, state.TaskState);
    }

    private sealed class StubStartupTaskAccessor : IAgentStartupTaskAccessor
    {
        private readonly IAgentStartupTask? _task;

        public StubStartupTaskAccessor(IAgentStartupTask? task)
        {
            _task = task;
        }

        public Task<IAgentStartupTask?> TryGetTaskAsync() => Task.FromResult(_task);
    }

    private sealed class StubStartupTask : IAgentStartupTask
    {
        public StubStartupTask(StartupTaskState state)
        {
            State = state;
        }

        public StartupTaskState State { get; private set; }

        public Task<StartupTaskState> RequestEnableAsync()
        {
            State = StartupTaskState.Enabled;
            return Task.FromResult(State);
        }

        public void Disable()
        {
            State = StartupTaskState.Disabled;
        }
    }
}
