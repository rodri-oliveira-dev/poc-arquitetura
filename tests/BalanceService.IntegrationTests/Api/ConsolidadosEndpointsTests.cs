using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using BalanceService.Api.Contracts;
using BalanceService.IntegrationTests.Infrastructure;
using BalanceService.IntegrationTests.Infrastructure.Security;
using BalanceService.Infrastructure.Persistence;
using BalanceService.Domain.Balances;


using Microsoft.Extensions.DependencyInjection;

namespace BalanceService.IntegrationTests.Api;

public sealed class ConsolidadosEndpointsTests : IClassFixture<BalanceApiFactory>
{
    private readonly HttpClient _client;
    private readonly BalanceApiFactory _factory;

    public ConsolidadosEndpointsTests(BalanceApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Period_should_return_400_when_from_invalid_format()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/periodo?merchantId=m1&from=bad&to=2026-02-12");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.Contains("from", body.Errors.Keys);
    }

    [Fact]
    public async Task Options_preflight_should_allow_contract_headers()
    {
        using var req = new HttpRequestMessage(HttpMethod.Options, "/v1/consolidados/periodo");
        req.Headers.Add("Origin", "http://localhost:5173");
        req.Headers.Add("Access-Control-Request-Method", "GET");
        req.Headers.Add("Access-Control-Request-Headers", "Idempotency-Key, X-Correlation-Id");

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.True(res.Headers.TryGetValues("Access-Control-Allow-Headers", out var values));
        var allowedHeaders = string.Join(",", values!).ToLowerInvariant();
        Assert.Contains("idempotency-key", allowedHeaders);
        Assert.Contains("x-correlation-id", allowedHeaders);
    }

    [Fact]
    public async Task Period_should_return_400_when_from_greater_than_to()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/periodo?merchantId=m1&from=2026-02-12&to=2026-02-10");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.NotEmpty(body.Errors);
    }

    [Fact]
    public async Task Period_should_return_400_when_date_range_exceeds_configured_limit()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/periodo?merchantId=m1&from=2026-02-01&to=2026-03-05");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.Contains("to", body.Errors.Keys);
    }

    [Fact]
    public async Task Daily_should_return_400_when_date_invalid_format()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read",
            merchantIds: "m-success-daily");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/diario/bad-date?merchantId=m1");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.Contains("date", body.Errors.Keys);
    }

    [Fact]
    public async Task Daily_should_return_400_when_required_query_field_is_missing()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/diario/2026-02-10");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await AssertValidationErrorResponseAsync(res);
        Assert.Contains("merchantId", body.Errors.Keys);
    }

    [Fact]
    public async Task Daily_should_return_200_with_totals_when_data_exists()
    {
        // Arrange
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read",
            merchantIds: "m-success-daily");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        const string merchantId = "m-success-daily";
        var date = new DateOnly(2026, 02, 14);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();

            var now = DateTimeOffset.UtcNow;
            var balance = new DailyBalance(merchantId, date, "BRL", now);
            balance.Apply(
                new LedgerEntryCreatedEvent(
                    Id: Guid.NewGuid().ToString("N"),
                    Type: "CREDIT",
                    Amount: "150.00",
                    CreatedAt: now,
                    MerchantId: merchantId,
                    OccurredAt: now,
                    Description: "seed-it",
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    ExternalReference: null),
                now);

            balance.Apply(
                new LedgerEntryCreatedEvent(
                    Id: Guid.NewGuid().ToString("N"),
                    Type: "DEBIT",
                    Amount: "-20.00",
                    CreatedAt: now,
                    MerchantId: merchantId,
                    OccurredAt: now,
                    Description: "seed-it",
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    ExternalReference: null),
                now);

            db.DailyBalances.Add(balance);
            await db.SaveChangesAsync();
        }

        // Act
        var res = await _client.GetAsync($"/v1/consolidados/diario/{date:yyyy-MM-dd}?merchantId={merchantId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<DailyBalanceResponse>();
        Assert.NotNull(body);
        Assert.Equal(merchantId, body!.MerchantId);
        Assert.Equal("2026-02-14", body.Date);
        Assert.Equal("BRL", body.Currency);
        Assert.Equal("150.00", body.TotalCredits);
        Assert.Equal("20.00", body.TotalDebits);
        Assert.Equal("130.00", body.NetBalance);
        Assert.False(string.IsNullOrWhiteSpace(body.AsOf));
        Assert.False(string.IsNullOrWhiteSpace(body.CalculatedAt));
    }

    [Fact]
    public async Task Period_should_return_200_with_totals_and_items_when_data_exists()
    {
        // Arrange
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read",
            merchantIds: "m-success-period");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        const string merchantId = "m-success-period";
        var from = new DateOnly(2026, 02, 10);
        var to = new DateOnly(2026, 02, 14);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();

            var now = DateTimeOffset.UtcNow;

            var day1 = new DailyBalance(merchantId, from, "BRL", now);
            day1.Apply(
                new LedgerEntryCreatedEvent(
                    Id: Guid.NewGuid().ToString("N"),
                    Type: "DEBIT",
                    Amount: "-20.00",
                    CreatedAt: now,
                    MerchantId: merchantId,
                    OccurredAt: now,
                    Description: "seed-it",
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    ExternalReference: null),
                now);

            var day2 = new DailyBalance(merchantId, to, "BRL", now);
            day2.Apply(
                new LedgerEntryCreatedEvent(
                    Id: Guid.NewGuid().ToString("N"),
                    Type: "CREDIT",
                    Amount: "150.00",
                    CreatedAt: now,
                    MerchantId: merchantId,
                    OccurredAt: now,
                    Description: "seed-it",
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    ExternalReference: null),
                now);

            db.DailyBalances.AddRange(day1, day2);
            await db.SaveChangesAsync();
        }

        // Act
        var res = await _client.GetAsync(
            $"/v1/consolidados/periodo?merchantId={merchantId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<PeriodBalanceResponse>();
        Assert.NotNull(body);
        Assert.Equal(merchantId, body!.MerchantId);
        Assert.Equal("2026-02-10", body.From);
        Assert.Equal("2026-02-14", body.To);
        Assert.Equal("BRL", body.Currency);
        Assert.Equal("150.00", body.TotalCredits);
        Assert.Equal("20.00", body.TotalDebits);
        Assert.Equal("130.00", body.NetBalance);
        Assert.Equal(2, body.Items.Count);
        Assert.Equal("2026-02-10", body.Items[0].Date);
        Assert.Equal("0.00", body.Items[0].TotalCredits);
        Assert.Equal("20.00", body.Items[0].TotalDebits);
        Assert.Equal("-20.00", body.Items[0].NetBalance);
        Assert.False(string.IsNullOrWhiteSpace(body.Items[0].AsOf));
        Assert.Equal("2026-02-14", body.Items[1].Date);
        Assert.Equal("150.00", body.Items[1].TotalCredits);
        Assert.Equal("0.00", body.Items[1].TotalDebits);
        Assert.Equal("150.00", body.Items[1].NetBalance);
        Assert.False(string.IsNullOrWhiteSpace(body.Items[1].AsOf));        Assert.False(string.IsNullOrWhiteSpace(body.CalculatedAt));
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
}
