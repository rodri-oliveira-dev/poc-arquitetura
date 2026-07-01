namespace TransferService.Api.Contracts.Responses;

public sealed record SolicitarTransferenciaResponse(
    Guid TransferenciaId,
    string Status,
    string SourceMerchantId,
    string DestinationMerchantId,
    decimal Amount,
    DateTimeOffset CreatedAt,
    string StatusUrl);
