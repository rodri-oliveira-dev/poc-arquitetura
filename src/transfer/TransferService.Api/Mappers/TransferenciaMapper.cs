using TransferService.Api.Contracts.Requests;
using TransferService.Api.Contracts.Responses;
using TransferService.Application.Transferencias.Commands;
using TransferService.Application.Transferencias.Queries;

namespace TransferService.Api.Mappers;

public static class TransferenciaMapper
{
    public static SolicitarTransferenciaCommand ToCommand(
        SolicitarTransferenciaRequest request,
        string idempotencyKey,
        string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new(
            idempotencyKey,
            request.SourceMerchantId,
            request.DestinationMerchantId,
            request.Amount!.Value,
            correlationId,
            request.Description,
            request.ExternalReference);
    }

    public static SolicitarTransferenciaResponse ToResponse(SolicitarTransferenciaResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new(
            result.TransferenciaId,
            result.Status,
            result.SourceMerchantId,
            result.DestinationMerchantId,
            result.Amount,
            result.CreatedAt,
            $"/api/v1/transferencias/{result.TransferenciaId}");
    }

    public static ObterStatusTransferenciaResponse ToResponse(ObterStatusTransferenciaResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new(
            result.TransferenciaId,
            result.Status,
            result.SourceMerchantId,
            result.DestinationMerchantId,
            result.Amount,
            result.CreatedAt,
            result.UpdatedAt);
    }
}
