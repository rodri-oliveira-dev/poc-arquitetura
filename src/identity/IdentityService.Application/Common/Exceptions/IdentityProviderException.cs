using System.Net;

namespace IdentityService.Application.Common.Exceptions;

public sealed class IdentityProviderException(
    IdentityProviderErrorKind kind,
    string message,
    HttpStatusCode? statusCode = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public IdentityProviderErrorKind Kind
    {
        get;
    } = kind;

    public HttpStatusCode? StatusCode
    {
        get;
    } = statusCode;
}
