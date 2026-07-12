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

        var payment = await _paymentRepository.GetByIdAsync(new PaymentId(request.PaymentId), cancellationToken)
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
#pragma warning disable IDE0046, IDE0075 // Sonar S3358 rejeita o ternario encadeado sugerido pelo formatador.
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new ConflictException("Idempotency-Key already used with a different refund payload.");
            }

            if (IsIncompleteRefundReplay(existing.Response))
            {
                return await CreateExternalRefundAsync(
                    new PaymentId(existing.Response.PaymentId),
                    new RefundId(existing.Response.RefundId),
                    idempotencyKey,
                    idempotentReplay: true,
                    cancellationToken);
            }
#pragma warning restore IDE0046, IDE0075

            return existing.Response with
            {
                IdempotentReplay = true
            };
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        payment = await _paymentRepository.GetByIdForUpdateAsync(new PaymentId(request.PaymentId), cancellationToken)
            ?? throw new NotFoundException("Payment nao encontrado.");

        if (!request.AuthorizedMerchantIds.Contains(payment.MerchantId.Value))
            throw new ForbiddenException("Token sem autorizacao para o merchant do Payment.");

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

        return await CreateExternalRefundAsync(
            payment.PaymentId,
            refund.RefundId,
            idempotencyKey,
            idempotentReplay: false,
            cancellationToken);
    }

    private async Task<RequestRefundResult> CreateExternalRefundAsync(
        PaymentId paymentId,
        RefundId refundId,
        string idempotencyKey,
        bool idempotentReplay,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadRefundSnapshotAsync(paymentId, refundId, cancellationToken);

        if (!snapshot.RequiresExternalCreation)
            return snapshot.Response with
            {
                IdempotentReplay = idempotentReplay
            };

        try
        {
            var externalResult = await _paymentGateway.CreateRefundAsync(
                new CreateExternalRefundRequest(
                    snapshot.PaymentId.Value,
                    snapshot.RefundId.Value,
                    snapshot.ProviderPaymentId,
                    snapshot.Amount.Amount,
                    snapshot.Amount.Currency.Code,
                    snapshot.Reason,
                    BuildExternalRefundIdempotencyKey(snapshot.PaymentId.Value, snapshot.RefundId.Value),
                    snapshot.CorrelationId,
                    snapshot.ExternalReference),
                cancellationToken);

            return await ApplyExternalRefundResultAsync(
                snapshot.PaymentId,
                snapshot.RefundId,
                idempotencyKey,
                externalResult,
                idempotentReplay,
                cancellationToken);
        }
        catch (PaymentGatewayException ex)
        {
            if (!ex.IsTransient)
            {
                await ApplyExternalRefundFailureAsync(
                    snapshot.PaymentId,
                    snapshot.RefundId,
                    idempotencyKey,
                    ex,
                    cancellationToken);
            }

            throw new ExternalPaymentProviderException(
                ex.Category,
                BuildSafeProviderMessage(ex.Category),
                ex.RetryAfter,
                ex);
        }
    }

    private async Task<RefundCreationSnapshot> LoadRefundSnapshotAsync(
        PaymentId paymentId,
        RefundId refundId,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {paymentId.Value} nao encontrado para refund.");
        var refund = payment.FindRefund(refundId)
            ?? throw new InvalidOperationException($"Refund {refundId.Value} nao encontrado.");

        return payment.ExternalPaymentReference is null
            ? throw new ConflictException("Payment nao possui referencia externa para refund.")
            : new RefundCreationSnapshot(
            payment.PaymentId,
            refund.RefundId,
            payment.ExternalPaymentReference.Value.Value,
            refund.Amount,
            refund.Reason,
            refund.CorrelationId,
            refund.ExternalReference,
            NeedsExternalCreation(refund),
            ToResult(payment, refund, idempotentReplay: false));
    }

    private async Task<RequestRefundResult> ApplyExternalRefundResultAsync(
        PaymentId paymentId,
        RefundId refundId,
        string idempotencyKey,
        CreateExternalRefundResult externalResult,
        bool idempotentReplay,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var payment = await _paymentRepository.GetByIdForUpdateAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {paymentId.Value} nao encontrado para refund.");
        var refund = payment.FindRefund(refundId)
            ?? throw new InvalidOperationException($"Refund {refundId.Value} nao encontrado.");

        ApplyExternalResult(payment, refund, externalResult);

        var response = ToResult(payment, refund, idempotentReplay);
        await _idempotencyService.UpdateRefundResponseAsync(
            payment.MerchantId.Value,
            idempotencyKey,
            response with
            {
                IdempotentReplay = false
            },
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private async Task ApplyExternalRefundFailureAsync(
        PaymentId paymentId,
        RefundId refundId,
        string idempotencyKey,
        PaymentGatewayException exception,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var payment = await _paymentRepository.GetByIdForUpdateAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {paymentId.Value} nao encontrado para refund.");
        var refund = payment.FindRefund(refundId)
            ?? throw new InvalidOperationException($"Refund {refundId.Value} nao encontrado.");

        if (CanMarkProviderFailed(refund))
        {
            payment.MarkRefundProviderFailed(
                _clock.UtcNow,
                refundId,
                refund.ProviderRefundId ?? $"refund:{refundId.Value:N}",
                exception.Code,
                "Provider refused refund.");
        }

        var failed = ToResult(payment, refund, idempotentReplay: false);
        await _idempotencyService.UpdateRefundResponseAsync(payment.MerchantId.Value, idempotencyKey, failed, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    private void ApplyExternalResult(
        Payment payment,
        PaymentRefund refund,
        CreateExternalRefundResult externalResult)
    {
        if (!NeedsExternalCreation(refund))
            return;

        if (string.Equals(externalResult.ProviderStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            if (CanMarkProviderFailed(refund))
                payment.MarkRefundProviderFailed(_clock.UtcNow, refund.RefundId, externalResult.ProviderRefundId, externalResult.RawStatus, "Provider reported refund failed.");

            return;
        }

        if (string.Equals(externalResult.ProviderStatus, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            payment.MarkRefundProviderSucceeded(_clock.UtcNow, refund.RefundId, externalResult.ProviderRefundId, externalResult.RawStatus);
            return;
        }

        payment.RegisterRefundProviderCreated(_clock.UtcNow, refund.RefundId, externalResult.ProviderRefundId, externalResult.RawStatus);
    }

    private static bool IsIncompleteRefundReplay(RequestRefundResult response)
        => string.IsNullOrWhiteSpace(response.ProviderRefundId)
            && string.Equals(response.Status, RefundStatus.Requested.ToString(), StringComparison.Ordinal);

    private static bool NeedsExternalCreation(PaymentRefund refund)
        => string.IsNullOrWhiteSpace(refund.ProviderRefundId)
            && refund.Status == RefundStatus.Requested;

    private static bool CanMarkProviderFailed(PaymentRefund refund)
        => refund.Status is RefundStatus.Requested or RefundStatus.ProviderPending;

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

#pragma warning disable IDE0072 // Categorias futuras devem cair na mensagem generica segura.
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
#pragma warning restore IDE0072

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record RefundCreationSnapshot(
        PaymentId PaymentId,
        RefundId RefundId,
        string ProviderPaymentId,
        Money Amount,
        string Reason,
        string? CorrelationId,
        string? ExternalReference,
        bool RequiresExternalCreation,
        RequestRefundResult Response);
}
