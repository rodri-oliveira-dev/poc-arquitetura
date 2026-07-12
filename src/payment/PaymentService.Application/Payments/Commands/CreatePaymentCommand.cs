using MediatR;

namespace PaymentService.Application.Payments.Commands;

public sealed record CreatePaymentCommand(
    string IdempotencyKey,
    string MerchantId,
    decimal Amount,
    string Currency,
    string? Description,
    string? ExternalReference,
    string? CorrelationId) : IRequest<CreatePaymentResult>;
