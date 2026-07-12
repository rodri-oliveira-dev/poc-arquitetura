using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using MediatR;

using PaymentService.Application.Abstractions.Gateway;
using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Commands;

public sealed class CreatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentIdempotencyService idempotencyService,
    IPaymentGateway paymentGateway,
    IUnitOfWork unitOfWork,
    IClock clock)
        : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPaymentRepository _paymentRepository = paymentRepository;
    private readonly IPaymentIdempotencyService _idempotencyService = idempotencyService;
    private readonly IPaymentGateway _paymentGateway = paymentGateway;
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
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw new ConflictException("Idempotency-Key already used with a different payload.");

            if (!string.IsNullOrWhiteSpace(existing.Response.ExternalPaymentReference))
            {
                return existing.Response with
                {
                    IdempotentReplay = true,
                    ClientSecret = null
                };
            }

            var existingPayment = await _paymentRepository.GetByIdAsync(
                    new PaymentId(existing.Response.PaymentId),
                    cancellationToken)
                ?? throw new ConflictException("Payment idempotente ainda nao esta disponivel para replay.");

            return await CompleteExternalCreationAsync(
                existingPayment,
                idempotencyKey,
                request.CorrelationId,
                idempotentReplay: true,
                cancellationToken);
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

        return await CompleteExternalCreationAsync(
            payment,
            idempotencyKey,
            request.CorrelationId,
            idempotentReplay: false,
            cancellationToken);
    }

    private static CreatePaymentResult ToResult(Payment payment, bool idempotentReplay)
        => new(
            payment.PaymentId.Value,
            payment.Status.ToString(),
            payment.MerchantId.Value,
            payment.Amount.Amount,
            payment.Amount.Currency.Code,
            payment.Provider.ToString(),
            payment.Description,
            payment.ExternalReference?.Value,
            payment.ExternalPaymentReference?.Value,
            payment.ProviderStatus,
            null,
            payment.LedgerEntryReference?.Value,
            payment.CreatedAt,
            payment.UpdatedAt,
            idempotentReplay);

    private async Task<CreatePaymentResult> CompleteExternalCreationAsync(
        Payment payment,
        string idempotencyKey,
        string? correlationId,
        bool idempotentReplay,
        CancellationToken cancellationToken)
    {
        try
        {
            var externalIdempotencyKey = BuildExternalIdempotencyKey(payment.PaymentId.Value);
            var externalResult = await _paymentGateway.CreatePaymentIntentAsync(
                new CreateExternalPaymentRequest(
                    payment.PaymentId.Value,
                    payment.MerchantId.Value,
                    payment.Amount.Amount,
                    payment.Amount.Currency.Code,
                    payment.Description,
                    payment.ExternalReference?.Value,
                    externalIdempotencyKey,
                    correlationId),
                cancellationToken);

            ApplyExternalResult(payment, externalResult, _clock.UtcNow);

            var response = ToResult(payment, idempotentReplay) with
            {
                ClientSecret = externalResult.ClientSecret
            };

            await _idempotencyService.UpdateResponseAsync(
                payment.MerchantId.Value,
                idempotencyKey,
                response with
                {
                    ClientSecret = null,
                    IdempotentReplay = false
                },
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return response;
        }
        catch (PaymentGatewayException ex)
        {
            if (!ex.IsTransient)
            {
                payment.MarkFailed(_clock.UtcNow, ex.Code ?? ex.Category.ToString());
                var failedResponse = ToResult(payment, idempotentReplay: false);
                await _idempotencyService.UpdateResponseAsync(
                    payment.MerchantId.Value,
                    idempotencyKey,
                    failedResponse,
                    cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            throw new ExternalPaymentProviderException(
                ex.Category,
                BuildSafeProviderMessage(ex.Category),
                ex.RetryAfter,
                ex);
        }
    }

    public static string BuildExternalIdempotencyKey(Guid paymentId)
        => $"payment:{paymentId:N}:stripe:create-payment-intent";

    private static void ApplyExternalResult(
        Payment payment,
        CreateExternalPaymentResult externalResult,
        DateTimeOffset now)
    {
        var reference = new ExternalPaymentReference(externalResult.ExternalPaymentReference);
        var providerStatus = externalResult.RawStatus ?? externalResult.ProviderStatus;

        if (externalResult.RequiresAction)
        {
            payment.MarkRequiresAction(now, reference, providerStatus);
            return;
        }

        if (string.Equals(externalResult.ProviderStatus, "processing", StringComparison.OrdinalIgnoreCase))
        {
            payment.MarkProcessing(now, reference, providerStatus);
            return;
        }

        payment.RegisterProviderIntent(now, reference, providerStatus);
    }

#pragma warning disable IDE0072 // Categorias futuras devem cair na mensagem generica segura.
    private static string BuildSafeProviderMessage(PaymentGatewayErrorCategory category)
        => category switch
        {
            PaymentGatewayErrorCategory.RateLimited => "Provider de pagamentos limitou a criacao da intencao externa.",
            PaymentGatewayErrorCategory.AuthenticationFailed => "Provider de pagamentos recusou a autenticacao configurada.",
            PaymentGatewayErrorCategory.InvalidRequest => "Provider de pagamentos recusou a criacao da intencao externa.",
            PaymentGatewayErrorCategory.PaymentRejected => "Provider de pagamentos rejeitou a criacao da intencao externa.",
            PaymentGatewayErrorCategory.Conflict => "Provider de pagamentos retornou conflito para a operacao externa.",
            PaymentGatewayErrorCategory.UnknownResult => "Timeout ao criar intencao externa; o resultado pode ter sido aplicado pelo provider.",
            PaymentGatewayErrorCategory.CircuitOpen => "Circuito do provider de pagamentos esta aberto.",
            _ => "Falha ao criar intencao externa no provider de pagamentos."
        };
#pragma warning restore IDE0072

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
