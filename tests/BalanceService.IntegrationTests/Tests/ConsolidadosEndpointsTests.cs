using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using BalanceService.Api.Contracts;
using BalanceService.IntegrationTests.Infrastructure;
using BalanceService.IntegrationTests.Infrastructure.Security;
using BalanceService.Infrastructure.Persistence;
using BalanceService.Domain.Balances;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace BalanceService.IntegrationTests.Tests;

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
            issuer: "https://auth-api",
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/periodo?merchantId=m1&from=bad&to=2026-02-12");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Options_preflight_should_allow_contract_headers()
    {
        using var req = new HttpRequestMessage(HttpMethod.Options, "/v1/consolidados/periodo");
        req.Headers.Add("Origin", "http://localhost:5173");
        req.Headers.Add("Access-Control-Request-Method", "GET");
        req.Headers.Add("Access-Control-Request-Headers", "Idempotency-Key, X-Correlation-Id");

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        res.Headers.TryGetValues("Access-Control-Allow-Headers", out var values).Should().BeTrue();
        var allowedHeaders = string.Join(",", values!).ToLowerInvariant();
        allowedHeaders.Should().Contain("idempotency-key");
        allowedHeaders.Should().Contain("x-correlation-id");
    }

    [Fact]
    public async Task Period_should_return_400_when_from_greater_than_to()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/periodo?merchantId=m1&from=2026-02-12&to=2026-02-10");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Daily_should_return_400_when_date_invalid_format()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "balance-api",
            scopes: "balance.read",
            merchantIds: "m-success-daily");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/diario/bad-date?merchantId=m1");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Daily_should_return_200_with_totals_when_data_exists()
    {
        // Arrange
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
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
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<DailyBalanceResponse>();
        body.Should().NotBeNull();
        body!.MerchantId.Should().Be(merchantId);
        body.Date.Should().Be("2026-02-14");
        body.Currency.Should().Be("BRL");
        body.TotalCredits.Should().Be("150.00");
        body.TotalDebits.Should().Be("20.00");
        body.NetBalance.Should().Be("130.00");
        body.AsOf.Should().NotBeNullOrWhiteSpace();
        body.CalculatedAt.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Period_should_return_200_with_totals_and_items_when_data_exists()
    {
        // Arrange
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
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
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<PeriodBalanceResponse>();
        body.Should().NotBeNull();
        body!.MerchantId.Should().Be(merchantId);
        body.From.Should().Be("2026-02-10");
        body.To.Should().Be("2026-02-14");
        body.Currency.Should().Be("BRL");
        body.TotalCredits.Should().Be("150.00");
        body.TotalDebits.Should().Be("20.00");
        body.NetBalance.Should().Be("130.00");
        body.Items.Should().HaveCount(2);

        body.Items[0].Date.Should().Be("2026-02-10");
        body.Items[0].TotalCredits.Should().Be("0.00");
        body.Items[0].TotalDebits.Should().Be("20.00");
        body.Items[0].NetBalance.Should().Be("-20.00");
        body.Items[0].AsOf.Should().NotBeNullOrWhiteSpace();

        body.Items[1].Date.Should().Be("2026-02-14");
        body.Items[1].TotalCredits.Should().Be("150.00");
        body.Items[1].TotalDebits.Should().Be("0.00");
        body.Items[1].NetBalance.Should().Be("150.00");
        body.Items[1].AsOf.Should().NotBeNullOrWhiteSpace();

        body.CalculatedAt.Should().NotBeNullOrWhiteSpace();
    }
}
