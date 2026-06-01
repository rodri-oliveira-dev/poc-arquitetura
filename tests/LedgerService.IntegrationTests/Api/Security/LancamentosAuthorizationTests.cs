using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;


using LedgerService.Api.Contracts.Responses;
using LedgerService.Application.Common.Models;
using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerService.IntegrationTests.Api.Security;

public sealed class LancamentosAuthorizationTests : IClassFixture<LedgerApiFactory>
{
    private readonly LedgerApiFactory _factory;
    private readonly HttpClient _client;

    public LancamentosAuthorizationTests(LedgerApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Get_should_return_401_without_token()
    {
        // LedgerService não expõe GET /lancamentos; o endpoint protegido é POST.
        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new
        {
            merchantId = "m1",
            type = "CREDIT",
            amount = 10.00m
        });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Post_should_return_403_when_scope_claim_is_missing()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: null);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new
        {
            merchantId = "m1",
            type = "CREDIT",
            amount = 10.00m
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_should_return_403_when_missing_write_scope()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Mesmo com token válido, sem o scope ledger.write, deve retornar 403.
        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new
        {
            merchantId = "m1",
            type = "CREDIT",
            amount = 10.00m
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_should_return_401_when_issuer_is_invalid()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://invalid-issuer",
            audiences: "ledger-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new
        {
            merchantId = "m1",
            type = "CREDIT",
            amount = 10.00m
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Post_should_return_401_when_audience_is_invalid()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new
        {
            merchantId = "m1",
            type = "CREDIT",
            amount = 10.00m
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Post_should_return_401_when_token_is_expired()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write",
            now: DateTimeOffset.UtcNow.AddMinutes(-20),
            lifetimeMinutes: 1);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new
        {
            merchantId = "m1",
            type = "CREDIT",
            amount = 10.00m
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Post_should_return_401_when_signature_is_invalid()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write",
            signWithUntrustedKey: true);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new
        {
            merchantId = "m1",
            type = "CREDIT",
            amount = 10.00m
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Post_should_return_403_when_token_is_not_authorized_for_requested_merchant()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write",
            merchantIds: "m2");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new { merchantId = "m1", type = "CREDIT", amount = 10.00m })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_should_return_403_when_token_has_no_merchant_claim()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write",
            merchantIds: null);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new { merchantId = "m1", type = "CREDIT", amount = 10.00m })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_should_return_413_when_request_body_exceeds_limit()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = new StringContent(
                """
                {
                  "merchantId": "m1",
                  "type": "CREDIT",
                  "amount": 10.00,
                  "description": "payload intentionally larger than the test request body limit to exercise operational API protection"
                }
                """,
                Encoding.UTF8,
                "application/json")
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
    }

    [Fact]
    public async Task Post_should_create_lancamento_with_write_scope()
    {
        var token = TestJwtTokenFactory.CreateToken();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var idempotencyKey = Guid.NewGuid().ToString();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new { merchantId = "tese", type = "CREDIT", amount = 10.00m })
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey);

        var res = await _client.SendAsync(req);

        // Contrato do endpoint: 201 + body (LancamentoDto) e header X-Correlation-Id
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.True(res.Headers.TryGetValues("X-Correlation-Id", out var correlationValues));
        var correlationId = correlationValues!.Single();
        Assert.True(Guid.TryParse(correlationId, out _));

        var body = await res.Content.ReadFromJsonAsync<LancamentoDto>();
        Assert.NotNull(body);
        Assert.StartsWith("lan_", body!.Id);
        Assert.Equal("lan_".Length + 8, body.Id.Length);
        Assert.Equal($"/api/v1/lancamentos/{body.Id}", res.Headers.Location?.ToString());
        Assert.Equal("tese", body.MerchantId);
        Assert.Equal("CREDIT", body.Type);
        Assert.Equal("10.00", body.Amount);
        Assert.Null(body.Description);
        Assert.Null(body.ExternalReference);
        Assert.True(DateTimeOffset.TryParse(body.OccurredAt, out _));
        Assert.True(DateTimeOffset.TryParse(body.CreatedAt, out _));
        // Idempotência (cenário de sucesso): mesma Idempotency-Key + mesmo payload deve fazer replay da resposta.
        using var replayReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new { merchantId = "tese", type = "CREDIT", amount = 10.00m })
        };
        replayReq.Headers.Add("Idempotency-Key", idempotencyKey);

        var replayRes = await _client.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.Created, replayRes.StatusCode);
        Assert.Equal(res.Headers.Location, replayRes.Headers.Location);
        var replayBody = await replayRes.Content.ReadFromJsonAsync<LancamentoDto>();
        Assert.NotNull(replayBody);
        Assert.Equivalent(body, replayBody!);
    }

    [Fact]
    public async Task Get_estornos_should_return_200_with_ledger_read_scope_and_keycloak_merchant()
    {
        var estorno = await SeedEstornoAsync("tese");
        var token = TestJwtTokenFactory.CreateToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{estorno.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ObterStatusEstornoLancamentoResponse>();
        Assert.NotNull(body);
        Assert.Equal(estorno.Id, body!.EstornoId);
    }

    [Fact]
    public async Task Post_should_return_400_when_amount_has_more_than_two_decimal_places()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new { merchantId = "m1", type = "CREDIT", amount = 10.123m })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.Contains("amount", body.Errors.Keys);
    }

    [Fact]
    public async Task Post_should_return_validation_contract_when_amount_type_is_invalid()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = new StringContent(
                """
                {
                  "merchantId": "m1",
                  "type": "CREDIT",
                  "amount": "abc"
                }
                """,
                Encoding.UTF8,
                "application/json")
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.Contains("amount", body.Errors.Keys);
    }

    [Fact]
    public async Task Post_should_return_validation_contract_when_json_is_malformed()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = new StringContent(
                """
                {
                  "merchantId": "m1",
                  "type": "CREDIT",
                  "amount": 10.00,
                """,
                Encoding.UTF8,
                "application/json")
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.NotEmpty(body.Errors);
        var raw = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("StackTrace", raw);
    }

    [Fact]
    public async Task Post_should_return_400_when_payload_is_absent()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos");
        req.Content = new StringContent("", Encoding.UTF8, "application/json");
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.Contains("$", body.Errors.Keys);
    }

    [Fact]
    public async Task Post_should_return_400_when_required_field_is_missing()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new { type = "CREDIT", amount = 10.00m })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.Contains("merchantId", body.Errors.Keys);
    }

    [Theory]
    [InlineData("desc changed", "ext")]
    [InlineData("desc", "ext changed")]
    public async Task Post_should_return_409_when_same_idempotency_key_is_reused_with_changed_description_or_external_reference(
        string description,
        string externalReference)
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var idempotencyKey = Guid.NewGuid().ToString();

        using var firstReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new
            {
                merchantId = "m1",
                type = "CREDIT",
                amount = 10.00m,
                description = "desc",
                externalReference = "ext"
            })
        };
        firstReq.Headers.Add("Idempotency-Key", idempotencyKey);

        var firstRes = await _client.SendAsync(firstReq);
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);
        using var replayReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new
            {
                merchantId = "m1",
                type = "CREDIT",
                amount = 10.00m,
                description,
                externalReference
            })
        };
        replayReq.Headers.Add("Idempotency-Key", idempotencyKey);

        var replayRes = await _client.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.Conflict, replayRes.StatusCode);
    }

    [Fact]
    public async Task Options_preflight_should_allow_contract_headers()
    {
        using var req = new HttpRequestMessage(HttpMethod.Options, "/api/v1/lancamentos");
        req.Headers.Add("Origin", "http://localhost:5173");
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "Idempotency-Key, X-Correlation-Id");

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.True(res.Headers.TryGetValues("Access-Control-Allow-Headers", out var values));
        var allowedHeaders = string.Join(",", values!).ToLowerInvariant();
        Assert.Contains("idempotency-key", allowedHeaders);
        Assert.Contains("x-correlation-id", allowedHeaders);
    }

    private static async Task<ValidationErrorResponse> AssertValidationErrorResponseAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<ValidationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("https://httpstatuses.com/400", body!.Type);
        Assert.Equal("Invalid request", body.Title);
        Assert.Equal(400, body.Status);
        Assert.Equal("One or more validation errors occurred.", body.Detail);
        Assert.NotEmpty(body.Errors);
        Assert.False(string.IsNullOrWhiteSpace(body.CorrelationId));
        return body;
    }

    private async Task<EstornoLancamento> SeedEstornoAsync(string merchantId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var estorno = new EstornoLancamento(
            Guid.NewGuid(),
            merchantId,
            "Erro operacional no lancamento original",
            Guid.NewGuid(),
            DateTime.UtcNow);

        await db.EstornosLancamentos.AddAsync(estorno);
        await db.SaveChangesAsync();

        return estorno;
    }
}
