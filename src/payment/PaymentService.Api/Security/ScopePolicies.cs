namespace PaymentService.Api.Security;

public static class ScopePolicies
{
    public const string ClaimType = "scope";

    public const string PaymentRead = "payment.read";
    public const string PaymentWrite = "payment.write";

    public const string PaymentReadPolicy = "scope:payment.read";
    public const string PaymentWritePolicy = "scope:payment.write";
}
