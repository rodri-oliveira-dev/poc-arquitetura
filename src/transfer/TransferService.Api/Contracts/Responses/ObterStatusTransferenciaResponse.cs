namespace TransferService.Api.Contracts.Responses;

public sealed record ObterStatusTransferenciaResponse(
    Guid TransferenciaId,
    string Status,
    string SourceMerchantId,
    string DestinationMerchantId,
    decimal Amount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
