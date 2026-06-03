using System.Net;
using System.Net.Sockets;

namespace Insighter.Agent;

internal interface IAgentPortResolver
{
    string ResolveApiBaseUrl();
}

internal sealed class AgentPortResolver : IAgentPortResolver
{
    private const int DefaultPort = 5078;
    private const int MaxAttempts = 10;

    public string ResolveApiBaseUrl()
    {
        for (var port = DefaultPort; port < DefaultPort + MaxAttempts; port++)
        {
            if (IsPortAvailable(port))
            {
                return $"http://127.0.0.1:{port}";
            }
        }

        throw new InvalidOperationException("Unable to find an available localhost port for Insighter.Agent.");
    }

    private static bool IsPortAvailable(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }
}
