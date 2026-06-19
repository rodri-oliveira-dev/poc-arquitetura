using System.Net;

namespace TransferService.Worker.Ledger;

public sealed class LedgerServiceException(HttpStatusCode statusCode, string responseBody) : Exception(BuildMessage(statusCode))
{
    public HttpStatusCode StatusCode
    {
        get;
    } = statusCode;
    public string ResponseBody
    {
        get;
    } = responseBody;

    public bool IsTransient
        => StatusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static string BuildMessage(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.Unauthorized
            ? "LedgerService.Api retornou 401 Unauthorized ao validar o token service-to-service do TransferService.Worker."
            : $"LedgerService.Api retornou {(int)statusCode} ({statusCode}).";
}
