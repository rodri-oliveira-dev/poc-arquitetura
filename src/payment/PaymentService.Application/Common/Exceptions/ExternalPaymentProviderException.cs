using PaymentService.Application.Abstractions.Gateway;

namespace PaymentService.Application.Common.Exceptions;

public sealed class ExternalPaymentProviderException(
    PaymentGatewayErrorCategory category,
    string message,
    TimeSpan? retryAfter = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public PaymentGatewayErrorCategory Category { get; } = category;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
