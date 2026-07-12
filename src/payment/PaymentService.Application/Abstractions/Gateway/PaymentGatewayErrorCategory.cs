namespace PaymentService.Application.Abstractions.Gateway;

public enum PaymentGatewayErrorCategory
{
    Transient = 1,
    RateLimited = 2,
    AuthenticationFailed = 3,
    InvalidRequest = 4,
    PaymentRejected = 5,
    Conflict = 6,
    UnknownResult = 7,
    CircuitOpen = 8,
    Unknown = 9
}
