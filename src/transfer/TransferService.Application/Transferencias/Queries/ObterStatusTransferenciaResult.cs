namespace TransferService.Application.Transferencias.Queries;

public sealed record ObterStatusTransferenciaResult(
    Guid TransferenciaId,
    string Status,
    string SourceMerchantId,
    string DestinationMerchantId,
    decimal Amount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
