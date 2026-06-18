namespace TransferService.Api.Contracts.Requests;

public sealed class SolicitarTransferenciaRequest
{
    public string SourceMerchantId { get; init; } = string.Empty;

    public string DestinationMerchantId { get; init; } = string.Empty;

    public decimal? Amount
    {
        get; init;
    }

    public string? Description
    {
        get; init;
    }

    public string? ExternalReference
    {
        get; init;
    }
}
