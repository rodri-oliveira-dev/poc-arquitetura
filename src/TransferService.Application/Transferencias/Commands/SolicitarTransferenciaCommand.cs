using MediatR;

namespace TransferService.Application.Transferencias.Commands;

public sealed record SolicitarTransferenciaCommand(
    string IdempotencyKey,
    string SourceMerchantId,
    string DestinationMerchantId,
    decimal Amount,
    string? CorrelationId = null,
    string? Description = null,
    string? ExternalReference = null) : IRequest<SolicitarTransferenciaResult>;
