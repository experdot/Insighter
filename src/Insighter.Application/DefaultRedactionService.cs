using Insighter.Domain;

namespace Insighter.Application;

public sealed class DefaultRedactionService : IRedactionService
{
    public Task<LogEvent> RedactAsync(LogEvent logEvent, CancellationToken cancellationToken)
        => Task.FromResult(logEvent);
}
