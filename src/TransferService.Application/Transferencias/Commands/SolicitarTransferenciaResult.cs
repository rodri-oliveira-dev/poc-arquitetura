namespace TransferService.Application.Transferencias.Commands;

public sealed record SolicitarTransferenciaResult(
    Guid TransferenciaId,
    string Status,
    string SourceMerchantId,
    string DestinationMerchantId,
    decimal Amount,
    DateTimeOffset CreatedAt,
    bool IdempotentReplay);
