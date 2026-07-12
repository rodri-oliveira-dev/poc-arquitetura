using System.Globalization;

using BalanceService.Application.Idempotency;
using BalanceService.Domain.Exceptions;

namespace BalanceService.UnitTests.Application.Idempotency;

public sealed class ProcessedEventTests
{
    [Fact]
    public void Ctor_should_store_event_identity_and_processing_timestamps()
    {
        var occurredAt = DateTimeOffset.Parse("2026-02-16T10:00:00-03:00", CultureInfo.InvariantCulture);
        var processedAt = DateTimeOffset.Parse("2026-02-16T13:00:05Z", CultureInfo.InvariantCulture);

        var processedEvent = new ProcessedEvent(
            eventId: "evt-1",
            merchantId: "merchant-1",
            occurredAt: occurredAt,
            processedAt: processedAt);

        Assert.Equal("evt-1", processedEvent.EventId);
        Assert.Equal("merchant-1", processedEvent.MerchantId);
        Assert.Equal(occurredAt, processedEvent.OccurredAt);
        Assert.Equal(processedAt, processedEvent.ProcessedAt);
    }

    [Theory]
    [InlineData("", "merchant-1", "EventId")]
    [InlineData(" ", "merchant-1", "EventId")]
    [InlineData("evt-1", "", "MerchantId")]
    [InlineData("evt-1", " ", "MerchantId")]
    public void Ctor_should_reject_missing_identity_fields(string eventId, string merchantId, string expectedMessage)
    {
        var occurredAt = DateTimeOffset.Parse("2026-02-16T10:00:00-03:00", CultureInfo.InvariantCulture);
        var processedAt = DateTimeOffset.Parse("2026-02-16T13:00:05Z", CultureInfo.InvariantCulture);

        ProcessedEvent act() => new ProcessedEvent(eventId, merchantId, occurredAt, processedAt);

        var ex = Assert.Throws<DomainException>((Func<ProcessedEvent>)act);
        Assert.Contains(expectedMessage, ex.Message);
    }
}
