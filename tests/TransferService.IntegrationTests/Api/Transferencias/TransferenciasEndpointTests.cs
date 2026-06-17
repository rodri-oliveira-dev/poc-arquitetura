using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using TransferService.Api.Contracts.Responses;
using TransferService.IntegrationTests.Infrastructure;
using TransferService.IntegrationTests.Infrastructure.Security;

namespace TransferService.IntegrationTests.Api.Transferencias;

[Trait("Category", "Integration")]
public sealed class TransferenciasEndpointTests : IClassFixture<TransferApiFactory>
{
    private readonly HttpClient _client;

    public TransferenciasEndpointTests(TransferApiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Post_transferencias_should_return_202_accepted()
    {
        Authenticate(scopes: "transfer.write");

        using var req = CreatePostRequest(Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SolicitarTransferenciaResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.TransferenciaId);
        Assert.Equal("Pending", body.Status);
        Assert.Equal("m1", body.SourceMerchantId);
        Assert.Equal("m2", body.DestinationMerchantId);
        Assert.Equal(100m, body.Amount);
    }

    [Fact]
    public async Task Post_transferencias_should_return_location_header()
    {
        Authenticate(scopes: "transfer.write");

        using var req = CreatePostRequest(Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SolicitarTransferenciaResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal($"/api/v1/transferencias/{body.TransferenciaId}", res.Headers.Location?.ToString());
        Assert.Equal(res.Headers.Location?.ToString(), body.StatusUrl);
    }

    [Fact]
    public async Task Post_transferencias_should_return_400_without_idempotency_key()
    {
        Authenticate(scopes: "transfer.write");

        using var req = CreatePostRequest(idempotencyKey: null);

        var res = await _client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.Contains(body.Errors.Keys, key => key.Contains("idempotency", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Post_transferencias_should_return_400_for_invalid_payload()
    {
        Authenticate(scopes: "transfer.write");

        using var req = CreatePostRequest(Guid.NewGuid().ToString(), amount: 0m);

        var res = await _client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.Contains("amount", body.Errors.Keys);
    }

    [Fact]
    public async Task Post_transferencias_should_return_403_when_merchant_is_not_authorized()
    {
        Authenticate(scopes: "transfer.write", merchantIds: "m9");

        using var req = CreatePostRequest(Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_transferencias_should_return_idempotent_response_for_same_key_and_payload()
    {
        Authenticate(scopes: "transfer.write");
        var idempotencyKey = Guid.NewGuid().ToString();

        using var firstReq = CreatePostRequest(idempotencyKey);
        var firstRes = await _client.SendAsync(firstReq, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, firstRes.StatusCode);
        var firstBody = await firstRes.Content.ReadFromJsonAsync<SolicitarTransferenciaResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(firstBody);

        using var replayReq = CreatePostRequest(idempotencyKey);
        var replayRes = await _client.SendAsync(replayReq, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, replayRes.StatusCode);
        var replayBody = await replayRes.Content.ReadFromJsonAsync<SolicitarTransferenciaResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(replayBody);

        Assert.Equivalent(firstBody, replayBody);
        Assert.Equal(firstRes.Headers.Location?.ToString(), replayRes.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Post_transferencias_should_return_409_for_same_key_and_different_payload()
    {
        Authenticate(scopes: "transfer.write");
        var idempotencyKey = Guid.NewGuid().ToString();

        using var firstReq = CreatePostRequest(idempotencyKey);
        var firstRes = await _client.SendAsync(firstReq, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, firstRes.StatusCode);

        using var conflictReq = CreatePostRequest(idempotencyKey, description: "Transferencia ajustada");
        var conflictRes = await _client.SendAsync(conflictReq, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, conflictRes.StatusCode);
    }

    [Fact]
    public async Task Get_transferencias_should_return_200_for_existing_saga()
    {
        var created = await CreateTransferenciaAsync();
        Authenticate(scopes: "transfer.read", merchantIds: "m1");

        var res = await _client.GetAsync($"/api/v1/transferencias/{created.TransferenciaId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ObterStatusTransferenciaResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(created.TransferenciaId, body.TransferenciaId);
        Assert.Equal("Pending", body.Status);
        Assert.Equal("m1", body.SourceMerchantId);
        Assert.Equal("m2", body.DestinationMerchantId);
        Assert.Equal(100m, body.Amount);
    }

    [Fact]
    public async Task Get_transferencias_should_return_404_for_missing_saga()
    {
        Authenticate(scopes: "transfer.read");

        var res = await _client.GetAsync($"/api/v1/transferencias/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Get_transferencias_should_return_401_without_authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var res = await _client.GetAsync($"/api/v1/transferencias/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Get_transferencias_should_return_403_without_transfer_read_scope()
    {
        var created = await CreateTransferenciaAsync();
        Authenticate(scopes: "transfer.write", merchantIds: "m1");

        var res = await _client.GetAsync($"/api/v1/transferencias/{created.TransferenciaId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private async Task<SolicitarTransferenciaResponse> CreateTransferenciaAsync()
    {
        Authenticate(scopes: "transfer.write", merchantIds: "m1");
        using var req = CreatePostRequest(Guid.NewGuid().ToString());
        var res = await _client.SendAsync(req, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<SolicitarTransferenciaResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        return created;
    }

    private void Authenticate(string scopes, string? merchantIds = "m1")
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: TestJwtTokenFactory.TransferAudience,
            scopes: scopes,
            merchantIds: merchantIds);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static HttpRequestMessage CreatePostRequest(
        string? idempotencyKey,
        decimal amount = 100m,
        string description = "Transferencia entre carteiras")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transferencias")
        {
            Content = JsonContent.Create(new
            {
                sourceMerchantId = "m1",
                destinationMerchantId = "m2",
                amount,
                description,
                externalReference = "pedido-123"
            })
        };

        if (idempotencyKey is not null)
            req.Headers.Add("Idempotency-Key", idempotencyKey);

        return req;
    }

    private static async Task<ValidationErrorResponse> AssertValidationErrorResponseAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<ValidationErrorResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal("https://httpstatuses.com/400", body.Type);
        Assert.Equal("Invalid request", body.Title);
        Assert.Equal(400, body.Status);
        Assert.Equal("One or more validation errors occurred.", body.Detail);
        Assert.NotEmpty(body.Errors);
        Assert.False(string.IsNullOrWhiteSpace(body.CorrelationId));
        return body;
    }
}
