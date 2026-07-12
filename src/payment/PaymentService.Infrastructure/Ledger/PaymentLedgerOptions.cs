namespace PaymentService.Infrastructure.Ledger;

public sealed class PaymentLedgerOptions
{
    public const string SectionName = "PaymentService:Ledger";

    public Uri? BaseAddress
    {
        get; set;
    }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public PaymentLedgerAuthOptions Auth { get; set; } = new();
}

public sealed class PaymentLedgerAuthOptions
{
    public Uri? TokenEndpoint
    {
        get; set;
    }

    public string ClientId { get; set; } = "poc-automation";

    public string ClientSecret { get; set; } = string.Empty;

    public string Scope { get; set; } = "ledger.write";

    public TimeSpan RefreshSkew { get; set; } = TimeSpan.FromMinutes(1);
}
