using System.Threading.Channels;
using Insighter.Domain;

namespace Insighter.Application;

public sealed class ChannelEventSink : IEventSink
{
    private readonly Channel<LogEvent> _channel;

    public ChannelEventSink(Channel<LogEvent> channel)
    {
        _channel = channel;
    }

    public ValueTask WriteAsync(LogEvent logEvent, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(logEvent, cancellationToken);
}
