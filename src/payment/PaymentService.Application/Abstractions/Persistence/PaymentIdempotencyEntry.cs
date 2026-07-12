using PaymentService.Application.Payments.Commands;

namespace PaymentService.Application.Abstractions.Persistence;

public sealed record PaymentIdempotencyEntry(
    string RequestHash,
    CreatePaymentResult Response);
