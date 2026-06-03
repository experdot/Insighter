using System.IO.Pipes;
using System.Text;

namespace Insighter.Agent;

internal interface IAgentSingleInstanceCoordinator : IAsyncDisposable
{
    bool IsPrimaryInstance { get; }

    Task<bool> TryAcquirePrimaryInstanceAsync(CancellationToken cancellationToken);

    Task SignalOpenUiAsync(CancellationToken cancellationToken);

    Task RunServerAsync(Func<string, CancellationToken, Task> onCommandAsync, CancellationToken cancellationToken);
}

internal sealed class AgentSingleInstanceCoordinator : IAgentSingleInstanceCoordinator
{
    private Mutex? _mutex;

    public bool IsPrimaryInstance { get; private set; }

    public Task<bool> TryAcquirePrimaryInstanceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _mutex = new Mutex(true, AgentConstants.MutexName, out var createdNew);
        IsPrimaryInstance = createdNew;
        return Task.FromResult(createdNew);
    }

    public async Task SignalOpenUiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                AgentConstants.PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            await client.ConnectAsync(3000, cancellationToken);
            var payload = Encoding.UTF8.GetBytes(AgentConstants.OpenUiCommand);
            await client.WriteAsync(payload, cancellationToken);
            await client.FlushAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
        }
        catch (IOException)
        {
        }
    }

    public async Task RunServerAsync(Func<string, CancellationToken, Task> onCommandAsync, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    AgentConstants.PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server, Encoding.UTF8);
                var command = await reader.ReadToEndAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(command))
                {
                    await onCommandAsync(command.Trim(), cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _mutex?.Dispose();
        _mutex = null;
        return ValueTask.CompletedTask;
    }
}
