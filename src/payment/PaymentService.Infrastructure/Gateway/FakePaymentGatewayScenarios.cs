namespace PaymentService.Infrastructure.Gateway;

public static class FakePaymentGatewayScenarios
{
    public const string Success = "Success";
    public const string RequiresAction = "RequiresAction";
    public const string Processing = "Processing";
    public const string DefinitiveFailure = "DefinitiveFailure";
    public const string Timeout = "Timeout";
    public const string RateLimit = "RateLimit";
    public const string TransientFailure = "TransientFailure";
    public const string RefundPending = "RefundPending";
    public const string RefundFailed = "RefundFailed";
}
