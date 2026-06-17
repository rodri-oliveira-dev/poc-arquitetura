using System.Net.Http.Json;
using System.Text.Json;

using TransferService.Worker.Options;

namespace TransferService.Worker.Ledger;

public sealed class LedgerServiceClient : ILedgerServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public LedgerServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LedgerLancamentoResult> CreateLancamentoAsync(
        CreateLedgerLancamentoRequest request,
        string idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/lancamentos")
        {
            Content = JsonContent.Create(new LedgerCreateLancamentoPayload(
                request.MerchantId,
                request.Type,
                request.Amount,
                request.Description,
                request.ExternalReference), options: JsonOptions)
        };

        AddHeaders(httpRequest, idempotencyKey, correlationId);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new LedgerServiceException(response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));

        var body = await response.Content.ReadFromJsonAsync<LedgerLancamentoResponse>(JsonOptions, cancellationToken)
            ?? throw new LedgerServiceException(response.StatusCode, "LedgerService.Api retornou resposta vazia para criacao de lancamento.");

        if (body.LancamentoId is not null)
            return new LedgerLancamentoResult(body.LancamentoId.Value);

        if (Guid.TryParse(body.Id, out var parsedId))
            return new LedgerLancamentoResult(parsedId);

        throw new LedgerServiceException(response.StatusCode, "LedgerService.Api nao retornou lancamentoId em formato UUID.");
    }

    public async Task<LedgerEstornoResult> SolicitarEstornoAsync(
        Guid lancamentoId,
        SolicitarLedgerEstornoRequest request,
        string idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/v1/lancamentos/{lancamentoId}/estornos")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        AddHeaders(httpRequest, idempotencyKey, correlationId);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new LedgerServiceException(response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));

        var body = await response.Content.ReadFromJsonAsync<LedgerEstornoResponse>(JsonOptions, cancellationToken)
            ?? throw new LedgerServiceException(response.StatusCode, "LedgerService.Api retornou resposta vazia para solicitacao de estorno.");

        return new LedgerEstornoResult(body.EstornoId);
    }

    private static void AddHeaders(HttpRequestMessage request, string idempotencyKey, string? correlationId)
    {
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        if (!string.IsNullOrWhiteSpace(correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
    }

    private sealed record LedgerCreateLancamentoPayload(
        string MerchantId,
        string Type,
        decimal Amount,
        string? Description,
        string? ExternalReference);

    private sealed record LedgerLancamentoResponse(string? Id, Guid? LancamentoId);

    private sealed record LedgerEstornoResponse(Guid EstornoId);
}
