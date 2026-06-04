using HotChocolate;
using HotChocolate.Execution;
using Insighter.Application;
using Insighter.Domain;

namespace Insighter.Hosting;

public sealed class AiLogQueryOperations
{
    public Task<AiLogQueryResult> AiQueryAsync(
        AiLogQueryRequest input,
        [Service] IAiLogQueryService service,
        CancellationToken cancellationToken)
        => service.ExecuteAsync(input, cancellationToken);
}

public sealed class GraphQlApiExceptionErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is not ApiException exception)
        {
            return error;
        }

        var builder = ErrorBuilder
            .New()
            .SetMessage(exception.Message)
            .SetCode(exception.Code);

        if (exception.Details is { Count: > 0 })
        {
            builder.SetExtension("details", exception.Details);
        }

        return builder.Build();
    }
}
