using TransferService.Application.Transferencias.Commands;

namespace TransferService.Application.Abstractions.Persistence;

public sealed record TransferenciaIdempotencyEntry(
    string RequestHash,
    SolicitarTransferenciaResult Response);
