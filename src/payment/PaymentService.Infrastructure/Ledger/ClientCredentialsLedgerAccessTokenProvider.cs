using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

namespace PaymentService.Infrastructure.Ledger;

public sealed class ClientCredentialsLedgerAccessTokenProvider : ILedgerAccessTokenProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<PaymentLedgerOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CachedAccessToken? _cachedToken;

    public ClientCredentialsLedgerAccessTokenProvider(
        HttpClient httpClient,
        IOptionsMonitor<PaymentLedgerOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _httpClient = httpClient;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue.Auth;
        var now = _timeProvider.GetUtcNow();
        if (_cachedToken is { } cached && cached.ExpiresAtUtc - options.RefreshSkew > now)
            return cached.AccessToken;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (_cachedToken is { } refreshed && refreshed.ExpiresAtUtc - options.RefreshSkew > now)
                return refreshed.AccessToken;

            _cachedToken = await RequestTokenAsync(options, cancellationToken);
            return _cachedToken.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<CachedAccessToken> RequestTokenAsync(
        PaymentLedgerAuthOptions options,
        CancellationToken cancellationToken)
    {
        if (options.TokenEndpoint is null)
            throw new LedgerAuthenticationException("PaymentService.Worker nao possui TokenEndpoint configurado para autenticar no LedgerService.Api.");

        using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(BuildTokenRequest(options))
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LedgerAuthenticationException($"Falha ao obter token para LedgerService.Api: HTTP {(int)response.StatusCode} ({response.StatusCode}). Resposta: {body}");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new LedgerAuthenticationException("Identity provider retornou resposta vazia ao emitir token para LedgerService.Api.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new LedgerAuthenticationException("Identity provider nao retornou access_token ao emitir token para LedgerService.Api.");

        var expiresAtUtc = _timeProvider.GetUtcNow().AddSeconds(Math.Max(token.ExpiresIn, 1));
        return new CachedAccessToken(token.AccessToken, expiresAtUtc);
    }

    private static IEnumerable<KeyValuePair<string, string>> BuildTokenRequest(PaymentLedgerAuthOptions options)
    {
        yield return new("grant_type", "client_credentials");
        yield return new("client_id", options.ClientId);
        yield return new("client_secret", options.ClientSecret);

        if (!string.IsNullOrWhiteSpace(options.Scope))
            yield return new("scope", options.Scope);
    }

    private sealed record CachedAccessToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    public void Dispose()
        => _refreshLock.Dispose();
}
