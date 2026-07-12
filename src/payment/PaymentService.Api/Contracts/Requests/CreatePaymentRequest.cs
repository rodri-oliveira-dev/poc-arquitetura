namespace PaymentService.Api.Contracts.Requests;

public sealed class CreatePaymentRequest
{
    public string MerchantId { get; init; } = string.Empty;

    public decimal? Amount
    {
        get; init;
    }

    public string Currency { get; init; } = string.Empty;

    public string? Description
    {
        get; init;
    }

    public string? ExternalReference
    {
        get; init;
    }
}
