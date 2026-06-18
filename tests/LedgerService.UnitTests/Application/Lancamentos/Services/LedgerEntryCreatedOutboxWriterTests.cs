using System.Text.Json;

using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Application.Lancamentos.Services;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;

using Moq;

namespace LedgerService.UnitTests.Application.Lancamentos.Services;

public sealed class LedgerEntryCreatedOutboxWriterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task WriteAsync_should_create_and_persist_ledger_entry_created_message()
    {
        var repository = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var occurredAt = new DateTime(2026, 2, 16, 12, 0, 0, DateTimeKind.Utc);
        var correlationId = Guid.NewGuid();
        var ledgerEntry = new LedgerEntry("m1", LedgerEntryType.Credit, 10.00m, occurredAt, "desc", "ext", correlationId, occurredAt);
        var response = new LancamentoDto(
            $"lan_{ledgerEntry.Id.ToString("N")[..8]}",
            ledgerEntry.Id,
            "m1",
            "CREDIT",
            "10.00",
            occurredAt.ToString("o"),
            "desc",
            "ext",
            occurredAt.ToString("o"));
        OutboxMessage? persisted = null;

        repository.Setup(x => x.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((message, _) => persisted = message)
            .Returns(Task.CompletedTask);

        var sut = new LedgerEntryCreatedOutboxWriter(repository.Object);

        await sut.WriteAsync(ledgerEntry, response, correlationId.ToString(), occurredAt, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal("LedgerEntry", persisted!.AggregateType);
        Assert.Equal(ledgerEntry.Id, persisted.AggregateId);
        Assert.Equal(LedgerEntryCreatedV2.EventType, persisted.EventType);
        Assert.Equal(correlationId, persisted.CorrelationId);
        Assert.Equal(occurredAt, persisted.OccurredAt);
        var payload = JsonSerializer.Deserialize<LedgerEntryCreatedV2>(persisted.Payload, JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(response.Id, payload!.Id);
        Assert.Equal(LedgerEntryCreatedEventFactory.SupportedCurrency, payload.Currency);
        Assert.Equal(correlationId.ToString(), payload.CorrelationId);
        repository.VerifyAll();
    }
}
