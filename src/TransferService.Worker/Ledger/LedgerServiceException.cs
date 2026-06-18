using System.Net;

namespace TransferService.Worker.Ledger;

public sealed class LedgerServiceException : Exception
{
    public LedgerServiceException(HttpStatusCode statusCode, string responseBody)
        : base($"LedgerService.Api retornou {(int)statusCode} ({statusCode}).")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode
    {
        get;
    }
    public string ResponseBody
    {
        get;
    }

    public bool IsTransient
        => StatusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
}
