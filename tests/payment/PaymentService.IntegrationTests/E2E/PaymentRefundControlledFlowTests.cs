using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using BalanceService.Application.IntegrationEvents;
using BalanceService.Domain.Balances;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PaymentService.Api.Contracts.Responses;
using PaymentService.Application.Abstractions.Ledger;
using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Payments.InboxProcessing;
using PaymentService.Application.Payments.Ledger;
using PaymentService.Domain.Payments;
using PaymentService.IntegrationTests.Infrastructure;
using PaymentService.IntegrationTests.Infrastructure.Security;

namespace PaymentService.IntegrationTests.E2E;

[Trait("Category", "E2E")]
[Trait("Category", "Integration")]
public sealed class PaymentRefundControlledFlowTests(PostgresPaymentFixture fixture) : IClassFixture<PostgresPaymentFixture>, IAsyncLifetime
{
    private readonly PostgresPaymentFixture _fixture = fixture;
    private ControlledLedgerGateway _ledger = null!;
    private PostgresPaymentApiFactory _factory = null!;

    private HttpClient Client
    {
        get => field ?? throw new InvalidOperationException("Client nao inicializado.");
        set;
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.CleanAsync();
        _ledger = new ControlledLedgerGateway();
        _factory = new PostgresPaymentApiFactory(
            _fixture.ConnectionString,
            services =>
            {
                services.RemoveAll<ILedgerEntryGateway>();
                services.AddSingleton<ILedgerEntryGateway>(_ledger);
            });

        Client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        _factory.Dispose();
    }

    [Fact]
    public async Task Payment_webhook_inbox_worker_ledger_and_balance_contract_should_complete_without_duplicates()
    {
        Authenticate("payment.write payment.read", "m1");
        var created = await CreatePaymentAsync("m1");
        Assert.NotNull(created.ProviderPaymentId);

        var payload = StripeWebhookTestData.CreatePayload(
            eventId: "evt_payment_e2e_succeeded",
            paymentIntentId: created.ProviderPaymentId,
            paymentId: created.PaymentId);

        var webhook = await SendWebhookAsync(payload);
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        await DrainInboxAsync();
        await ProcessLedgerAsync();

        var completed = await GetSavedPaymentAsync(created.PaymentId);
        Assert.Equal(PaymentStatus.Completed, completed.Status);
        Assert.NotNull(completed.LedgerEntryReference);
        Assert.Single(_ledger.CreditRequests);

        var duplicateWebhook = await SendWebhookAsync(payload);
        Assert.Equal(HttpStatusCode.OK, duplicateWebhook.StatusCode);
        await DrainInboxAsync();
        await ProcessLedgerAsync();

        Assert.Single(_ledger.CreditRequests);
        await using (var db = _fixture.CreateDbContext())
        {
            Assert.Equal(1, await db.InboxMessages.CountAsync(TestContext.Current.CancellationToken));
        }

        var movement = CreateBalanceMovementFromLedgerCredit(_ledger.CreditRequests.Single(), completed.LedgerEntryReference.Value.Value);
        var balance = new DailyBalance(movement.MerchantId, movement.Date, movement.Currency.Code, DateTimeOffset.UtcNow);
        balance.Apply(movement, DateTimeOffset.UtcNow);

        Assert.Equal("m1", balance.MerchantId);
        Assert.Equal(100m, balance.TotalCredits);
        Assert.Equal(0m, balance.TotalDebits);
        Assert.Equal(100m, balance.NetBalance);
    }

    [Fact]
    public async Task Refund_webhook_worker_ledger_reversal_and_balance_contract_should_complete_once()
    {
        Authenticate("payment.write payment.read payment.refund", "m1");
        var created = await CreateCompletedPaymentAsync();
        var refundExternalReference = $"refund-{Guid.NewGuid():N}";

        using var refundRequest = CreateRefundRequest(
            created.PaymentId,
            Guid.NewGuid().ToString(),
            amount: 100m,
            refundExternalReference);
        var refundResponse = await Client.SendAsync(refundRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, refundResponse.StatusCode);
        var refund = await refundResponse.Content.ReadFromJsonAsync<RequestRefundResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(refund);

        var paymentBeforeRefundWebhook = await GetSavedPaymentAsync(created.PaymentId);
        var refundEntity = paymentBeforeRefundWebhook.Refunds.Single();
        var refundWebhook = await SendWebhookAsync(StripeWebhookTestData.CreateRefundPayload(
            eventId: "evt_refund_e2e_succeeded",
            eventType: "refund.updated",
            paymentIntentId: paymentBeforeRefundWebhook.ExternalPaymentReference!.Value.Value,
            paymentId: created.PaymentId,
            refundId: refund.RefundId,
            providerRefundId: refundEntity.ProviderRefundId!,
            status: "succeeded"));
        Assert.Equal(HttpStatusCode.OK, refundWebhook.StatusCode);

        await DrainInboxAsync();
        await ProcessLedgerAsync();

        var refunded = await GetSavedPaymentAsync(created.PaymentId);
        Assert.Equal(PaymentStatus.Refunded, refunded.Status);
        Assert.Single(_ledger.ReversalRequests);

        using var duplicateReplayRequest = CreateRefundRequest(
            created.PaymentId,
            refundRequest.Headers.GetValues("Idempotency-Key").Single(),
            amount: 100m,
            refundExternalReference);
        var duplicateReplay = await Client.SendAsync(duplicateReplayRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, duplicateReplay.StatusCode);

        using var conflictRequest = CreateRefundRequest(
            created.PaymentId,
            refundRequest.Headers.GetValues("Idempotency-Key").Single(),
            amount: 99m,
            refundExternalReference);
        var conflict = await Client.SendAsync(conflictRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        await ProcessLedgerAsync();
        Assert.Single(_ledger.ReversalRequests);

        var reversal = _ledger.ReversalRequests.Single();
        var movement = CreateBalanceMovementFromLedgerReversal(_ledger.CreditRequests.Single(), reversal);
        var balance = new DailyBalance(movement.MerchantId, movement.Date, movement.Currency.Code, DateTimeOffset.UtcNow);
        balance.Apply(CreateBalanceMovementFromLedgerCredit(_ledger.CreditRequests.Single(), reversal.OriginalLedgerEntryReference.Value), DateTimeOffset.UtcNow);
        balance.Apply(movement, DateTimeOffset.UtcNow);

        Assert.Equal(100m, balance.TotalCredits);
        Assert.Equal(100m, balance.TotalDebits);
        Assert.Equal(0m, balance.NetBalance);
    }

    [Fact]
    public async Task Partial_refund_should_be_rejected_without_provider_or_ledger_side_effect()
    {
        Authenticate("payment.write payment.refund", "m1");
        var created = await CreateCompletedPaymentAsync();

        using var request = CreateRefundRequest(created.PaymentId, Guid.NewGuid().ToString(), amount: 50m);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(_ledger.ReversalRequests);
    }

    [Fact]
    public async Task Ledger_unknown_result_should_retry_with_same_idempotency_key_and_complete_once()
    {
        Authenticate("payment.write payment.read", "m1");
        _ledger.CreditOutcomes.Enqueue(LedgerEntryCreationResult.UnknownResult("simulated timeout"));
        _ledger.CreditOutcomes.Enqueue(LedgerEntryCreationResult.Success(new LedgerEntryReference(Guid.Parse("99999999-9999-9999-9999-999999999999"))));

        var created = await CreatePaymentAsync("m1");
        await SendWebhookAsync(StripeWebhookTestData.CreatePayload(
            eventId: "evt_payment_timeout_retry",
            paymentIntentId: created.ProviderPaymentId!,
            paymentId: created.PaymentId));
        await DrainInboxAsync();

        await ProcessLedgerAsync();
        var pending = await GetSavedPaymentAsync(created.PaymentId);
        Assert.Equal(PaymentStatus.LedgerPending, pending.Status);
        Assert.Equal(LedgerIntegrationStatus.RetryScheduled, pending.LedgerIntegrationStatus);

        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE payment.payments
                SET ledger_next_retry_at_utc = NOW() - INTERVAL '1 second',
                    ledger_locked_until_utc = NULL,
                    ledger_lock_owner = NULL
                WHERE id = {0}
                """,
                [created.PaymentId],
                TestContext.Current.CancellationToken);
        }

        await ProcessLedgerAsync();
        var completed = await GetSavedPaymentAsync(created.PaymentId);
        Assert.Equal(PaymentStatus.Completed, completed.Status);
        Assert.Equal(2, _ledger.CreditRequests.Count);
        Assert.Equal(_ledger.CreditRequests[0].IdempotencyKey, _ledger.CreditRequests[1].IdempotencyKey);
    }

    private async Task<CreatePaymentResponse> CreateCompletedPaymentAsync()
    {
        var created = await CreatePaymentAsync("m1");
        await SendWebhookAsync(StripeWebhookTestData.CreatePayload(
            eventId: $"evt_{Guid.NewGuid():N}",
            paymentIntentId: created.ProviderPaymentId!,
            paymentId: created.PaymentId));
        await DrainInboxAsync();
        await ProcessLedgerAsync();
        return created;
    }

    private async Task<CreatePaymentResponse> CreatePaymentAsync(string merchantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments")
        {
            Content = JsonContent.Create(new
            {
                merchantId,
                amount = 100m,
                currency = "BRL",
                description = "Pagamento E2E controlado",
                externalReference = $"pedido-{Guid.NewGuid():N}"
            })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreatePaymentResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        return body;
    }

    private static HttpRequestMessage CreateRefundRequest(
        Guid paymentId,
        string idempotencyKey,
        decimal amount,
        string? externalReference = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payments/{paymentId}/refunds")
        {
            Content = JsonContent.Create(new
            {
                amount,
                reason = "requested_by_customer",
                externalReference = externalReference ?? $"refund-{Guid.NewGuid():N}"
            })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        return request;
    }

    private async Task<HttpResponseMessage> SendWebhookAsync(string payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", StripeWebhookTestData.CreateSignatureHeader(payload));
        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task DrainInboxAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IPaymentInboxRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var claimed = await inbox.ClaimEligibleAsync(
            10,
            DateTimeOffset.UtcNow,
            "e2e-worker",
            TimeSpan.FromMinutes(1),
            TestContext.Current.CancellationToken);

        foreach (var message in claimed)
            await sender.Send(new ProcessPaymentInboxMessageCommand(message.Id, "e2e-worker"), TestContext.Current.CancellationToken);
    }

    private async Task ProcessLedgerAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IPaymentLedgerProcessor>();
        await processor.ProcessBatchAsync(10, "e2e-ledger-worker", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
    }

    private async Task<Payment> GetSavedPaymentAsync(Guid paymentId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Payments
            .Include(x => x.Refunds)
            .SingleAsync(x => x.PaymentId == new PaymentId(paymentId), TestContext.Current.CancellationToken);
    }

    private void Authenticate(string scopes, string? merchantIds)
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: TestJwtTokenFactory.PaymentAudience,
            scopes: scopes,
            merchantIds: merchantIds);

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static BalanceMovement CreateBalanceMovementFromLedgerCredit(LedgerCreditRequest request, Guid ledgerEntryId)
    {
        var evt = new LedgerEntryCreatedIntegrationEvent(
            ledgerEntryId.ToString("D"),
            "CREDIT",
            request.Amount.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            request.Amount.Currency.Code,
            DateTimeOffset.UtcNow,
            request.MerchantId.Value,
            DateTimeOffset.UtcNow,
            request.Description,
            request.CorrelationId ?? Guid.NewGuid().ToString(),
            request.ExternalReference);

        return LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(evt);
    }

    private static BalanceMovement CreateBalanceMovementFromLedgerReversal(LedgerCreditRequest originalCredit, LedgerReversalRequest request)
    {
        var evt = new LedgerEntryCreatedIntegrationEvent(
            request.RefundId.Value.ToString("D"),
            "DEBIT",
            "-100.00",
            "BRL",
            DateTimeOffset.UtcNow,
            originalCredit.MerchantId.Value,
            DateTimeOffset.UtcNow,
            request.Reason,
            request.CorrelationId ?? Guid.NewGuid().ToString(),
            $"payment-refund:{request.RefundId.Value:D}");

        return LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(evt);
    }

    private sealed class ControlledLedgerGateway : ILedgerEntryGateway
    {
        public List<LedgerCreditRequest> CreditRequests { get; } = [];

        public List<LedgerReversalRequest> ReversalRequests { get; } = [];

        public Queue<LedgerEntryCreationResult> CreditOutcomes { get; } = [];

        public Task<LedgerEntryCreationResult> CreateCreditAsync(LedgerCreditRequest request, CancellationToken cancellationToken)
        {
            CreditRequests.Add(request);
            return Task.FromResult(CreditOutcomes.Count > 0
                ? CreditOutcomes.Dequeue()
                : LedgerEntryCreationResult.Success(new LedgerEntryReference(Guid.NewGuid())));
        }

        public Task<LedgerReversalRequestResult> RequestReversalAsync(LedgerReversalRequest request, CancellationToken cancellationToken)
        {
            ReversalRequests.Add(request);
            return Task.FromResult(LedgerReversalRequestResult.Accepted(Guid.NewGuid()));
        }
    }
}
