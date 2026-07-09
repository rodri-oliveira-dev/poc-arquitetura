namespace PaymentService.Application.Abstractions.Gateway;

public sealed class PaymentGatewayException(
    PaymentGatewayErrorCategory category,
    string safeMessage,
    string? code = null,
    TimeSpan? retryAfter = null,
    Exception? innerException = null)
    : Exception(safeMessage, innerException)
{
    public PaymentGatewayErrorCategory Category { get; } = category;

    public string? Code { get; } = code;

    public TimeSpan? RetryAfter { get; } = retryAfter;

    public bool IsTransient
        => Category is PaymentGatewayErrorCategory.Transient
            or PaymentGatewayErrorCategory.RateLimited
            or PaymentGatewayErrorCategory.UnknownResult
            or PaymentGatewayErrorCategory.CircuitOpen
            or PaymentGatewayErrorCategory.Unknown;
}
