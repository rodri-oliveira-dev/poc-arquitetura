using System.Net.Http.Headers;

namespace PaymentService.Infrastructure.Ledger;

public sealed class LedgerAuthenticationHandler(ILedgerAccessTokenProvider tokenProvider) : DelegatingHandler
{
    private readonly ILedgerAccessTokenProvider _tokenProvider = tokenProvider;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
