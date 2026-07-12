using MediatR;

namespace PaymentService.Application.Payments.Commands;

public sealed record RequestRefundCommand(
    Guid PaymentId,
    string IdempotencyKey,
    decimal? Amount,
    string Reason,
    string? ExternalReference,
    string? CorrelationId,
    IReadOnlyCollection<string> AuthorizedMerchantIds) : IRequest<RequestRefundResult>;
