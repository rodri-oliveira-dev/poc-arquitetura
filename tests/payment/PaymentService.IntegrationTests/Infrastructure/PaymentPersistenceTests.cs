using Microsoft.EntityFrameworkCore;

using PaymentService.Domain.Payments;
using PaymentService.Infrastructure.Persistence;

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
}
