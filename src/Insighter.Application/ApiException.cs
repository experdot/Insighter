using System.Net;

namespace Insighter.Application;

public sealed class ApiException : Exception
{
    public ApiException(HttpStatusCode statusCode, string code, string message, IReadOnlyDictionary<string, string[]>? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }

    public HttpStatusCode StatusCode { get; }

    public string Code { get; }

    public IReadOnlyDictionary<string, string[]>? Details { get; }
}
