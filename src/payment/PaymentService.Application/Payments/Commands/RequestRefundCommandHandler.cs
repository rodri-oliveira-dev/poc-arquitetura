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

public sealed class RequestRefundCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentIdempotencyService idempotencyService,
    IPaymentGateway paymentGateway,
    IUnitOfWork unitOfWork,
    IClock clock)
    : IRequestHandler<RequestRefundCommand, RequestRefundResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPaymentRepository _paymentRepository = paymentRepository;
    private readonly IPaymentIdempotencyService _idempotencyService = idempotencyService;
    private readonly IPaymentGateway _paymentGateway = paymentGateway;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IClock _clock = clock;

    public async Task<RequestRefundResult> Handle(RequestRefundCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var payment = await _paymentRepository.GetByIdForUpdateAsync(new PaymentId(request.PaymentId), cancellationToken)
            ?? throw new NotFoundException("Payment nao encontrado.");

        if (!request.AuthorizedMerchantIds.Contains(payment.MerchantId.Value))
            throw new ForbiddenException("Token sem autorizacao para o merchant do Payment.");

        var amount = request.Amount ?? payment.Amount.Amount;
        var reason = Normalize(request.Reason) ?? "requested_by_customer";
        var externalReference = Normalize(request.ExternalReference);
        var idempotencyKey = request.IdempotencyKey.Trim();
        var requestHash = GenerateRequestHash(payment.PaymentId.Value, amount, reason, externalReference);

        var existing = await _idempotencyService.GetRefundAsync(payment.MerchantId.Value, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw new ConflictException("Idempotency-Key already used with a different refund payload.");

            return existing.Response with { IdempotentReplay = true };
        }

        var now = _clock.UtcNow;
        var refund = payment.RequestRefund(
            RefundId.New(),
            new Money(amount, payment.Amount.Currency),
            reason,
            externalReference,
            request.CorrelationId,
            now);

        var response = ToResult(payment, refund, idempotentReplay: false);
        await _idempotencyService.AddRefundAsync(
            payment.MerchantId.Value,
            idempotencyKey,
            requestHash,
            response,
            now.AddDays(7),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await CreateExternalRefundAsync(payment.PaymentId, refund.RefundId, idempotencyKey, cancellationToken);
    }

    private async Task<RequestRefundResult> CreateExternalRefundAsync(
        PaymentId paymentId,
        RefundId refundId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdForUpdateAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {paymentId.Value} nao encontrado para refund.");
        var refund = payment.FindRefund(refundId)
            ?? throw new InvalidOperationException($"Refund {refundId.Value} nao encontrado.");

        if (payment.ExternalPaymentReference is null)
            throw new ConflictException("Payment nao possui referencia externa para refund.");

        try
        {
            var externalResult = await _paymentGateway.CreateRefundAsync(
                new CreateExternalRefundRequest(
                    payment.PaymentId.Value,
                    refund.RefundId.Value,
                    payment.ExternalPaymentReference.Value.Value,
                    refund.Amount.Amount,
                    refund.Amount.Currency.Code,
                    refund.Reason,
                    BuildExternalRefundIdempotencyKey(payment.PaymentId.Value, refund.RefundId.Value),
                    refund.CorrelationId,
                    refund.ExternalReference),
                cancellationToken);

            if (string.Equals(externalResult.ProviderStatus, "failed", StringComparison.OrdinalIgnoreCase))
                payment.MarkRefundProviderFailed(_clock.UtcNow, refundId, externalResult.ProviderRefundId, externalResult.RawStatus, "Provider reported refund failed.");
            else if (string.Equals(externalResult.ProviderStatus, "succeeded", StringComparison.OrdinalIgnoreCase))
                payment.MarkRefundProviderSucceeded(_clock.UtcNow, refundId, externalResult.ProviderRefundId, externalResult.RawStatus);
            else
                payment.RegisterRefundProviderCreated(_clock.UtcNow, refundId, externalResult.ProviderRefundId, externalResult.RawStatus);

            var response = ToResult(payment, refund, idempotentReplay: false);
            await _idempotencyService.UpdateRefundResponseAsync(payment.MerchantId.Value, idempotencyKey, response, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return response;
        }
        catch (PaymentGatewayException ex)
        {
            if (!ex.IsTransient)
            {
                payment.MarkRefundProviderFailed(_clock.UtcNow, refundId, refund.ProviderRefundId ?? $"refund:{refundId.Value:N}", ex.Code, "Provider refused refund.");
                var failed = ToResult(payment, refund, idempotentReplay: false);
                await _idempotencyService.UpdateRefundResponseAsync(payment.MerchantId.Value, idempotencyKey, failed, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            throw new ExternalPaymentProviderException(
                ex.Category,
                BuildSafeProviderMessage(ex.Category),
                ex.RetryAfter,
                ex);
        }
    }

    public static string BuildExternalRefundIdempotencyKey(Guid paymentId, Guid refundId)
        => $"payment:{paymentId:N}:refund:{refundId:N}:stripe:create-refund";

    private static RequestRefundResult ToResult(Payment payment, PaymentRefund refund, bool idempotentReplay)
        => new(
            payment.PaymentId.Value,
            refund.RefundId.Value,
            refund.Status.ToString(),
            refund.Amount.Amount,
            refund.Amount.Currency.Code,
            refund.Reason,
            refund.ExternalReference,
            refund.ProviderRefundId,
            refund.ProviderStatus,
            refund.LedgerReversalId,
            refund.CreatedAt,
            refund.UpdatedAt,
            idempotentReplay);

    private static string GenerateRequestHash(Guid paymentId, decimal amount, string reason, string? externalReference)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            PaymentId = paymentId,
            Amount = amount,
            Reason = reason,
            ExternalReference = externalReference
        }, JsonOptions);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildSafeProviderMessage(PaymentGatewayErrorCategory category)
        => category switch
        {
            PaymentGatewayErrorCategory.RateLimited => "Provider de pagamentos limitou a criacao do refund.",
            PaymentGatewayErrorCategory.AuthenticationFailed => "Provider de pagamentos recusou a autenticacao configurada.",
            PaymentGatewayErrorCategory.InvalidRequest => "Provider de pagamentos recusou o refund.",
            PaymentGatewayErrorCategory.Conflict => "Provider de pagamentos retornou conflito para o refund.",
            PaymentGatewayErrorCategory.UnknownResult => "Timeout ao criar refund; o resultado pode ter sido aplicado pelo provider.",
            PaymentGatewayErrorCategory.CircuitOpen => "Circuito do provider de pagamentos esta aberto.",
            _ => "Falha ao criar refund no provider de pagamentos."
        };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
