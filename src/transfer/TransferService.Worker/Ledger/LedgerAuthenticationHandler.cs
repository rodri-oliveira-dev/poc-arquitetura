using System.Net.Http.Headers;

namespace TransferService.Worker.Ledger;

public sealed class LedgerAuthenticationHandler : DelegatingHandler
{
    private readonly ILedgerAccessTokenProvider _tokenProvider;

    public LedgerAuthenticationHandler(ILedgerAccessTokenProvider tokenProvider)
    {
        ArgumentNullException.ThrowIfNull(tokenProvider);

        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
