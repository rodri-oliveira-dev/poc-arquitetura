using System.Globalization;

using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;
using FluentAssertions;

namespace BalanceService.UnitTests.Domain.Balances;

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

        processedEvent.EventId.Should().Be("evt-1");
        processedEvent.MerchantId.Should().Be("merchant-1");
        processedEvent.OccurredAt.Should().Be(occurredAt);
        processedEvent.ProcessedAt.Should().Be(processedAt);
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

        var act = () => new ProcessedEvent(eventId, merchantId, occurredAt, processedAt);

        act.Should().Throw<DomainException>().WithMessage($"*{expectedMessage}*");
    }
}
