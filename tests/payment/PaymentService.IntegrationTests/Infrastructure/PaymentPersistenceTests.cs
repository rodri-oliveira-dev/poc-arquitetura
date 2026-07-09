using Microsoft.EntityFrameworkCore;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Domain.Payments;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Persistence.Repositories;

namespace PaymentService.IntegrationTests.Infrastructure;

[Trait("Category", "Integration")]
public sealed class PaymentPersistenceTests(PostgresPaymentFixture fixture) : IClassFixture<PostgresPaymentFixture>, IAsyncLifetime
{
    private readonly PostgresPaymentFixture _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        await _fixture.CleanAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migration_should_create_payment_tables()
    {
        await using var db = _fixture.CreateDbContext();

        var tables = await db.Database.SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'payment' ORDER BY table_name")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("payments", tables);
        Assert.Contains("idempotency_records", tables);
        Assert.Contains("inbox_messages", tables);
    }

    [Fact]
    public async Task Payment_should_persist_and_load_with_expected_mappings()
    {
        var now = new DateTimeOffset(2026, 7, 9, 10, 0, 0, TimeSpan.Zero);
        var payment = new Payment(
            PaymentId.New(),
            new MerchantId("m1"),
            new Money(123.45m, Currency.Brl),
            PaymentProvider.Stripe,
            now,
            "Pagamento",
            new ExternalReference("order-123"));
        payment.MarkSucceeded(now.AddMinutes(1), new ExternalPaymentReference("pi_123"), "succeeded");
        payment.MarkCompleted(now.AddMinutes(2), new LedgerEntryReference(Guid.Parse("11111111-1111-1111-1111-111111111111")));

        await using (var db = _fixture.CreateDbContext())
        {
            await db.Payments.AddAsync(payment, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var saved = await db.Payments.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(payment.PaymentId, saved.PaymentId);
            Assert.Equal("m1", saved.MerchantId.Value);
            Assert.Equal(123.45m, saved.Amount.Amount);
            Assert.Equal("BRL", saved.Amount.Currency.Code);
            Assert.Equal(PaymentProvider.Stripe, saved.Provider);
            Assert.Equal(PaymentStatus.Completed, saved.Status);
            Assert.Equal("order-123", saved.ExternalReference?.Value);
            Assert.Equal("pi_123", saved.ExternalPaymentReference?.Value);
            Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), saved.LedgerEntryReference?.Value);
            Assert.Equal(now, saved.CreatedAt);
            Assert.Equal(now.AddMinutes(2), saved.UpdatedAt);
            Assert.Equal(now.AddMinutes(2), saved.CompletedAt);
        }
    }

    [Fact]
    public async Task Idempotency_records_should_enforce_unique_merchant_key()
    {
        await using var db = _fixture.CreateDbContext();
        db.IdempotencyRecords.Add(new PaymentIdempotencyRecord(
            Guid.NewGuid(),
            "m1",
            "key-1",
            "hash-1",
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1)));
        db.IdempotencyRecords.Add(new PaymentIdempotencyRecord(
            Guid.NewGuid(),
            "m1",
            "key-1",
            "hash-1",
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1)));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Inbox_messages_should_persist_payload_and_initial_status()
    {
        var payload = StripeWebhookTestData.CreatePayload();
        var paymentId = PaymentId.New();
        var message = PaymentInboxMessage.CreateStripe(
            "evt_123",
            "payment_intent.succeeded",
            payload,
            DateTimeOffset.UtcNow,
            "correlation-1",
            "pi_123",
            paymentId);

        await using (var db = _fixture.CreateDbContext())
        {
            await db.InboxMessages.AddAsync(message, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var saved = await db.InboxMessages.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("evt_123", saved.ProviderEventId);
            Assert.Equal("payment_intent.succeeded", saved.EventType);
            Assert.Equal(payload, saved.Payload);
            Assert.Equal(PaymentInboxStatus.Pending, saved.Status);
            Assert.Equal(StripeWebhookEventCategory.Supported, saved.EventCategory);
            Assert.Equal("pi_123", saved.ProviderPaymentId);
            Assert.Equal(paymentId, saved.PaymentId);
            Assert.Equal(64, saved.PayloadSha256.Length);
            Assert.Equal(0, saved.AttemptCount);
        }
    }

    [Fact]
    public async Task Inbox_repository_should_return_duplicate_for_same_provider_event()
    {
        await using var db = _fixture.CreateDbContext();
        var repository = new PaymentInboxRepository(db);
        var first = PaymentInboxMessage.CreateStripe(
            "evt_duplicate",
            "payment_intent.succeeded",
            StripeWebhookTestData.CreatePayload("evt_duplicate"),
            DateTimeOffset.UtcNow,
            null,
            null,
            null);
        var second = PaymentInboxMessage.CreateStripe(
            "evt_duplicate",
            "payment_intent.succeeded",
            StripeWebhookTestData.CreatePayload("evt_duplicate"),
            DateTimeOffset.UtcNow,
            null,
            null,
            null);

        var firstResult = await repository.StoreAsync(first, TestContext.Current.CancellationToken);
        var secondResult = await repository.StoreAsync(second, TestContext.Current.CancellationToken);

        Assert.Equal(PaymentInboxStoreResult.Inserted, firstResult);
        Assert.Equal(PaymentInboxStoreResult.Duplicate, secondResult);
        Assert.Equal(1, await db.InboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Inbox_repository_should_deduplicate_concurrent_inserts()
    {
        const int attempts = 12;
        var tasks = Enumerable.Range(0, attempts)
            .Select(async _ =>
            {
                await using var db = _fixture.CreateDbContext();
                var repository = new PaymentInboxRepository(db);
                var message = PaymentInboxMessage.CreateStripe(
                    "evt_concurrent",
                    "payment_intent.succeeded",
                    StripeWebhookTestData.CreatePayload("evt_concurrent"),
                    DateTimeOffset.UtcNow,
                    null,
                    null,
                    null);
                return await repository.StoreAsync(message, TestContext.Current.CancellationToken);
            });

        var results = await Task.WhenAll(tasks);

        await using var assertionDb = _fixture.CreateDbContext();
        Assert.Equal(1, results.Count(result => result == PaymentInboxStoreResult.Inserted));
        Assert.Equal(attempts - 1, results.Count(result => result == PaymentInboxStoreResult.Duplicate));
        Assert.Equal(1, await assertionDb.InboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }
}
