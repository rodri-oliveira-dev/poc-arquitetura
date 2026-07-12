using PaymentService.Api.Contracts.Requests;
using PaymentService.Api.Contracts.Responses;
using PaymentService.Application.Payments.Commands;
using PaymentService.Application.Payments.Queries;

namespace PaymentService.Api.Mappers;

public static class PaymentMapper
{
    public static CreatePaymentCommand ToCommand(
        CreatePaymentRequest request,
        string idempotencyKey,
        string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new(
            idempotencyKey,
            request.MerchantId,
            request.Amount!.Value,
            request.Currency,
            request.Description,
            request.ExternalReference,
            correlationId);
    }

    public static CreatePaymentResponse ToCreateResponse(CreatePaymentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new(
            result.PaymentId,
            result.Status,
            result.MerchantId,
            result.Amount,
            result.Currency,
            result.Provider,
            result.ExternalPaymentReference,
            result.ProviderStatus,
            result.IdempotentReplay ? null : result.ClientSecret,
            result.ExternalReference,
            $"/api/v1/payments/{result.PaymentId}");
    }

    public static PaymentResponse ToResponse(PaymentDetailsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new(
            result.PaymentId,
            result.Status,
            result.MerchantId,
            result.Amount,
            result.Currency,
            result.Provider,
            result.Description,
            result.ExternalReference,
            result.ExternalPaymentReference,
            result.LedgerEntryId,
            result.ProviderStatus,
            result.CreatedAt,
            result.UpdatedAt,
            result.CompletedAt);
    }

    public static RequestRefundResponse ToRefundResponse(RequestRefundResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new(
            result.PaymentId,
            result.RefundId,
            result.Status,
            result.Amount,
            result.Currency,
            result.Reason,
            result.ExternalReference,
            $"/api/v1/payments/{result.PaymentId}");
    }
}
