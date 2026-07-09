using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using MediatR;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Commands;

public sealed class CreatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentIdempotencyService idempotencyService,
    IUnitOfWork unitOfWork,
    IClock clock)
        : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPaymentRepository _paymentRepository = paymentRepository;
    private readonly IPaymentIdempotencyService _idempotencyService = idempotencyService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IClock _clock = clock;

    public async Task<CreatePaymentResult> Handle(
        CreatePaymentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var merchantId = request.MerchantId.Trim();
        var currency = request.Currency.Trim().ToUpperInvariant();
        var description = Normalize(request.Description);
        var externalReference = Normalize(request.ExternalReference);
        var idempotencyKey = request.IdempotencyKey.Trim();
        var requestHash = GenerateRequestHash(merchantId, request.Amount, currency, description, externalReference);

        var existing = await _idempotencyService.GetAsync(merchantId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal)
                ? existing.Response with
                {
                    IdempotentReplay = true
                }
                : throw new ConflictException("Idempotency-Key already used with a different payload.");
        }

        var now = _clock.UtcNow;
        var payment = new Payment(
            PaymentId.New(),
            new MerchantId(merchantId),
            new Money(request.Amount, new Currency(currency)),
            PaymentProvider.Stripe,
            now,
            description,
            externalReference is null ? null : new ExternalReference(externalReference));

        var response = ToResult(payment, idempotentReplay: false);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _paymentRepository.AddAsync(payment, cancellationToken);
        await _idempotencyService.AddAsync(
            merchantId,
            idempotencyKey,
            requestHash,
            response,
            now.AddDays(7),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return response;
    }

    private static CreatePaymentResult ToResult(Payment payment, bool idempotentReplay)
        => new(
            payment.PaymentId.Value,
            payment.Status.ToString(),
            payment.MerchantId.Value,
            payment.Amount.Amount,
            payment.Amount.Currency.Code,
            payment.Description,
            payment.ExternalReference?.Value,
            payment.ExternalPaymentReference?.Value,
            payment.LedgerEntryReference?.Value,
            payment.CreatedAt,
            payment.UpdatedAt,
            idempotentReplay);

    private static string GenerateRequestHash(
        string merchantId,
        decimal amount,
        string currency,
        string? description,
        string? externalReference)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            MerchantId = merchantId,
            Amount = amount,
            Currency = currency,
            Description = description,
            ExternalReference = externalReference
        }, JsonOptions);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
