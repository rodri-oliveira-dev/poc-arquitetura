using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using PaymentService.Application.Abstractions.Ledger;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Ledger;

public sealed class LedgerHttpGateway(HttpClient httpClient) : ILedgerEntryGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = httpClient;

    public async Task<LedgerEntryCreationResult> CreateCreditAsync(
        LedgerCreditRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/lancamentos")
            {
                Content = JsonContent.Create(
                    new CreateLedgerEntryPayload(
                        request.MerchantId.Value,
                        "CREDIT",
                        request.Amount.Amount,
                        request.Description,
                        request.ExternalReference),
                    options: JsonOptions)
            };

            httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey.ToString());
            if (!string.IsNullOrWhiteSpace(request.CorrelationId))
                httpRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return await MapFailureAsync(response, cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<LedgerEntryResponse>(JsonOptions, cancellationToken);
            return TryReadLedgerEntryReference(body, out var ledgerEntryReference)
                ? LedgerEntryCreationResult.Success(ledgerEntryReference)
                : LedgerEntryCreationResult.Definitive(
                    LedgerEntryFailureCategory.UnexpectedResponse,
                    "LedgerService.Api returned success without a valid ledger entry identifier.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return LedgerEntryCreationResult.UnknownResult("LedgerService.Api request timed out with unknown result.");
        }
        catch (HttpRequestException)
        {
            return LedgerEntryCreationResult.Transient(
                LedgerEntryFailureCategory.Network,
                "Network failure while calling LedgerService.Api.");
        }
        catch (LedgerAuthenticationException exception)
        {
            return LedgerEntryCreationResult.Transient(
                LedgerEntryFailureCategory.Authentication,
                string.IsNullOrWhiteSpace(exception.Message)
                    ? "Service-to-service authentication failed before calling LedgerService.Api."
                    : exception.Message);
        }
        catch (Exception exception) when (IsCircuitOpen(exception))
        {
            return LedgerEntryCreationResult.Transient(
                LedgerEntryFailureCategory.CircuitOpen,
                "LedgerService.Api circuit breaker is open.");
        }
    }

    public async Task<LedgerReversalRequestResult> RequestReversalAsync(
        LedgerReversalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/v1/lancamentos/{request.OriginalLedgerEntryReference.Value:D}/estornos")
            {
                Content = JsonContent.Create(
                    new RequestLedgerReversalPayload(request.Reason),
                    options: JsonOptions)
            };

            httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey.ToString());
            if (!string.IsNullOrWhiteSpace(request.CorrelationId))
                httpRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return await MapReversalFailureAsync(response, cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<LedgerReversalResponse>(JsonOptions, cancellationToken);
            return body?.EstornoId is { } estornoId
                ? LedgerReversalRequestResult.Accepted(estornoId)
                : LedgerReversalRequestResult.Definitive(
                    LedgerEntryFailureCategory.UnexpectedResponse,
                    "LedgerService.Api returned success without a valid reversal identifier.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return LedgerReversalRequestResult.UnknownResult("LedgerService.Api reversal request timed out with unknown result.");
        }
        catch (HttpRequestException)
        {
            return LedgerReversalRequestResult.Transient(
                LedgerEntryFailureCategory.Network,
                "Network failure while requesting LedgerService.Api reversal.");
        }
        catch (LedgerAuthenticationException exception)
        {
            return LedgerReversalRequestResult.Transient(
                LedgerEntryFailureCategory.Authentication,
                string.IsNullOrWhiteSpace(exception.Message)
                    ? "Service-to-service authentication failed before calling LedgerService.Api."
                    : exception.Message);
        }
        catch (Exception exception) when (IsCircuitOpen(exception))
        {
            return LedgerReversalRequestResult.Transient(
                LedgerEntryFailureCategory.CircuitOpen,
                "LedgerService.Api circuit breaker is open.");
        }
    }

    private static async Task<LedgerEntryCreationResult> MapFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var safeError = $"LedgerService.Api returned HTTP {(int)response.StatusCode} ({response.StatusCode}).";
        var retryAfter = response.Headers.RetryAfter?.Delta;

        if (response.StatusCode == HttpStatusCode.RequestTimeout)
            return LedgerEntryCreationResult.UnknownResult("LedgerService.Api request timed out with unknown result.");

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return LedgerEntryCreationResult.Transient(LedgerEntryFailureCategory.RateLimited, safeError, retryAfter);

        if (response.StatusCode >= HttpStatusCode.InternalServerError)
            return LedgerEntryCreationResult.Transient(LedgerEntryFailureCategory.ServiceUnavailable, safeError);

        var detailedError = await ReadSafeErrorAsync(response, safeError, cancellationToken);
#pragma warning disable IDE0072
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => LedgerEntryCreationResult.Definitive(LedgerEntryFailureCategory.Authentication, detailedError),
            HttpStatusCode.Forbidden => LedgerEntryCreationResult.Definitive(LedgerEntryFailureCategory.Authorization, detailedError),
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => LedgerEntryCreationResult.Definitive(LedgerEntryFailureCategory.Validation, detailedError),
            HttpStatusCode.NotFound => LedgerEntryCreationResult.Definitive(LedgerEntryFailureCategory.NotFound, detailedError),
            HttpStatusCode.Conflict => LedgerEntryCreationResult.Definitive(LedgerEntryFailureCategory.IdempotencyConflict, detailedError),
            _ => LedgerEntryCreationResult.Definitive(LedgerEntryFailureCategory.UnexpectedResponse, safeError)
        };
#pragma warning restore IDE0072
    }

    private static async Task<LedgerReversalRequestResult> MapReversalFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var safeError = $"LedgerService.Api returned HTTP {(int)response.StatusCode} ({response.StatusCode}) for reversal.";
        var retryAfter = response.Headers.RetryAfter?.Delta;

        if (response.StatusCode == HttpStatusCode.RequestTimeout)
            return LedgerReversalRequestResult.UnknownResult("LedgerService.Api reversal request timed out with unknown result.");

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return LedgerReversalRequestResult.Transient(LedgerEntryFailureCategory.RateLimited, safeError, retryAfter);

        if (response.StatusCode >= HttpStatusCode.InternalServerError)
            return LedgerReversalRequestResult.Transient(LedgerEntryFailureCategory.ServiceUnavailable, safeError);

        var detailedError = await ReadSafeErrorAsync(response, safeError, cancellationToken);
#pragma warning disable IDE0072
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => LedgerReversalRequestResult.Definitive(LedgerEntryFailureCategory.Authentication, detailedError),
            HttpStatusCode.Forbidden => LedgerReversalRequestResult.Definitive(LedgerEntryFailureCategory.Authorization, detailedError),
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => LedgerReversalRequestResult.Definitive(LedgerEntryFailureCategory.Validation, detailedError),
            HttpStatusCode.NotFound => LedgerReversalRequestResult.Definitive(LedgerEntryFailureCategory.NotFound, detailedError),
            HttpStatusCode.Conflict => LedgerReversalRequestResult.Definitive(LedgerEntryFailureCategory.IdempotencyConflict, detailedError),
            _ => LedgerReversalRequestResult.Definitive(LedgerEntryFailureCategory.UnexpectedResponse, safeError)
        };
#pragma warning restore IDE0072
    }

    private static async Task<string> ReadSafeErrorAsync(
        HttpResponseMessage response,
        string fallback,
        CancellationToken cancellationToken)
    {
        await response.Content.LoadIntoBufferAsync(cancellationToken);
        return fallback;
    }

    private static bool TryReadLedgerEntryReference(
        LedgerEntryResponse? body,
        out LedgerEntryReference ledgerEntryReference)
    {
        if (body?.LancamentoId is { } lancamentoId)
        {
            ledgerEntryReference = new LedgerEntryReference(lancamentoId);
            return true;
        }

        if (Guid.TryParse(body?.Id, out var legacyId))
        {
            ledgerEntryReference = new LedgerEntryReference(legacyId);
            return true;
        }

        ledgerEntryReference = default;
        return false;
    }

    private static bool IsCircuitOpen(Exception exception)
        => exception.GetType().Name.Contains("Circuit", StringComparison.OrdinalIgnoreCase)
           || exception.GetType().Name.Contains("Broken", StringComparison.OrdinalIgnoreCase);

    private sealed record CreateLedgerEntryPayload(
        string MerchantId,
        string Type,
        decimal Amount,
        string Description,
        string ExternalReference);

    private sealed record LedgerEntryResponse(string? Id, Guid? LancamentoId);

    private sealed record RequestLedgerReversalPayload(string Motivo);

    private sealed record LedgerReversalResponse(Guid EstornoId);
}
