namespace PaymentService.Application.Payments.InboxProcessing;

public sealed record ProviderEventMappingResult(
    PaymentProviderEvent? Event,
    bool IsPermanentFailure,
    string? Reason)
{
    public static ProviderEventMappingResult Success(PaymentProviderEvent providerEvent)
        => new(providerEvent, false, null);

    public static ProviderEventMappingResult PermanentFailure(string reason)
        => new(null, true, reason);
}
