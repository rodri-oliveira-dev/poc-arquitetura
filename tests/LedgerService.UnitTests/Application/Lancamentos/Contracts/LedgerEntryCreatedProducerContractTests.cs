using System.Text.Json;

using Json.Schema;

using LedgerService.Application.Abstractions.Time;
using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Application.Lancamentos.Services;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.UnitTests.Fixtures;
using Moq;

namespace LedgerService.UnitTests.Application.Lancamentos.Contracts;

public sealed class LedgerEntryCreatedProducerContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<JsonSchema> Schema = new(LoadSchemaCore);

    [Fact]
    public async Task CreateLancamento_should_persist_outbox_payload_that_matches_ledger_entry_created_v2_schema()
    {
        var outbox = await ProduceLedgerEntryCreatedOutboxAsync("CREDIT", "10.00");

        Assert.Equal("LedgerEntry", outbox.AggregateType);
        Assert.Equal(LedgerEntryCreatedV2.EventType, outbox.EventType);
        Assert.Equal("LedgerEntryCreated", GetEventName(outbox.EventType));
        Assert.Equal("v2", GetEventVersion(outbox.EventType));
        Assert.NotEqual(Guid.Empty, outbox.Id);
        Assert.NotEqual(Guid.Empty, outbox.AggregateId);
        Assert.Equal(Guid.Parse(ExpectedCorrelationId), outbox.CorrelationId);
        Assert.Equal(ExpectedOccurredAt, outbox.OccurredAt);

        JsonElement payload = ParsePayload(outbox.Payload);
        Assert.Equal(ExpectedMerchantId, payload.GetProperty("merchantId").GetString());
        Assert.Equal(ExpectedCorrelationId, payload.GetProperty("correlationId").GetString());
        Assert.Equal("BRL", payload.GetProperty("currency").GetString());
        AssertPayloadMatchesSchema(payload);
    }

    [Fact]
    public async Task CreateLancamento_should_persist_debit_outbox_payload_that_matches_ledger_entry_created_v2_schema()
    {
        var outbox = await ProduceLedgerEntryCreatedOutboxAsync("DEBIT", "-15.50");

        JsonElement payload = ParsePayload(outbox.Payload);

        Assert.Equal("DEBIT", payload.GetProperty("type").GetString());
        Assert.Equal("-15.50", payload.GetProperty("amount").GetString());
        AssertPayloadMatchesSchema(payload);
    }

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public void Ledger_entry_created_schema_should_reject_contract_breaks(string reason, string payload)
    {
        JsonElement node = ParsePayload(payload);

        EvaluationResults result = LoadSchema().Evaluate(node, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        Assert.False(result.IsValid, reason);
    }

    public static TheoryData<string, string> InvalidPayloads()
        => new()
        {
            {
                "required currency is missing",
                """
                {
                  "id": "lan_12345678",
                  "type": "CREDIT",
                  "amount": "10.00",
                  "createdAt": "2026-02-16T12:00:00.0000000Z",
                  "merchantId": "merchant-1",
                  "occurredAt": "2026-02-16T12:00:00.0000000Z",
                  "correlationId": "11111111-1111-1111-1111-111111111111"
                }
                """
            },
            {
                "required merchantId is missing",
                """
                {
                  "id": "lan_12345678",
                  "type": "CREDIT",
                  "amount": "10.00",
                  "currency": "BRL",
                  "createdAt": "2026-02-16T12:00:00.0000000Z",
                  "occurredAt": "2026-02-16T12:00:00.0000000Z",
                  "correlationId": "11111111-1111-1111-1111-111111111111"
                }
                """
            },
            {
                "amount must be a string",
                """
                {
                  "id": "lan_12345678",
                  "type": "CREDIT",
                  "amount": 10.00,
                  "currency": "BRL",
                  "createdAt": "2026-02-16T12:00:00.0000000Z",
                  "merchantId": "merchant-1",
                  "occurredAt": "2026-02-16T12:00:00.0000000Z",
                  "correlationId": "11111111-1111-1111-1111-111111111111"
                }
                """
            },
            {
                "event name is not part of the payload contract",
                """
                {
                  "id": "lan_12345678",
                  "type": "CREDIT",
                  "amount": "10.00",
                  "currency": "BRL",
                  "createdAt": "2026-02-16T12:00:00.0000000Z",
                  "merchantId": "merchant-1",
                  "occurredAt": "2026-02-16T12:00:00.0000000Z",
                  "correlationId": "11111111-1111-1111-1111-111111111111",
                  "eventName": "WrongEvent"
                }
                """
            },
            {
                "event version is not part of the payload contract",
                """
                {
                  "id": "lan_12345678",
                  "type": "CREDIT",
                  "amount": "10.00",
                  "currency": "BRL",
                  "createdAt": "2026-02-16T12:00:00.0000000Z",
                  "merchantId": "merchant-1",
                  "occurredAt": "2026-02-16T12:00:00.0000000Z",
                  "correlationId": "11111111-1111-1111-1111-111111111111",
                  "eventVersion": "v2"
                }
                """
            }
        };

    private const string ExpectedMerchantId = "merchant-contract";
    private const string ExpectedCorrelationId = "11111111-1111-1111-1111-111111111111";
    private static readonly DateTime ExpectedOccurredAt = new(2026, 2, 16, 12, 0, 0, DateTimeKind.Utc);

    private static async Task<OutboxMessage> ProduceLedgerEntryCreatedOutboxAsync(string type, string amount)
    {
        var ledgerRepo = new Mock<ILedgerEntryRepository>(MockBehavior.Strict);
        var idemRepo = new Mock<IIdempotencyRecordRepository>(MockBehavior.Strict);
        var outboxRepo = new Mock<IOutboxMessageRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var tx = new Mock<IAppTransaction>(MockBehavior.Strict);

        var input = LancamentoFixture.ValidInput(
            merchantId: ExpectedMerchantId,
            type: type,
            amount: amount,
            correlationId: ExpectedCorrelationId);

        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        idemRepo.Setup(x => x.GetByMerchantAndKeyAsync(input.MerchantId, input.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyRecord?)null);
        ledgerRepo.Setup(x => x.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        idemRepo.Setup(x => x.AddAsync(It.IsAny<IdempotencyRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        OutboxMessage? createdOutbox = null;
        outboxRepo.Setup(x => x.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((message, _) => createdOutbox = message)
            .Returns(Task.CompletedTask);

        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tx.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = new CreateLancamentoCommandHandler(
            ledgerRepo.Object,
            new CreateLancamentoIdempotencyService(idemRepo.Object),
            new LedgerEntryCreatedOutboxWriter(outboxRepo.Object),
            uow.Object,
            new FixedClock(ExpectedOccurredAt));

        await sut.Handle(new CreateLancamentoCommand(input), CancellationToken.None);

        Assert.NotNull(createdOutbox);
        return createdOutbox!;
    }

    private static JsonElement ParsePayload(string payload)
    {
        using JsonDocument document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }

    private static void AssertPayloadMatchesSchema(JsonElement payload)
    {
        EvaluationResults result = LoadSchema().Evaluate(payload, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        Assert.True(result.IsValid, JsonSerializer.Serialize(result, JsonOptions));
    }

    private static JsonSchema LoadSchema()
        => Schema.Value;

    private static JsonSchema LoadSchemaCore()
    {
        string schemaPath = Path.Combine(
            FindRepositoryRoot(),
            "contracts",
            "events",
            "ledger-entry-created.v2.schema.json");

        return JsonSchema.FromText(File.ReadAllText(schemaPath));
    }

    private static string GetEventName(string eventType)
        => eventType.Split('.', 2)[0];

    private static string GetEventVersion(string eventType)
        => eventType.Split('.', 2)[1];

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LedgerService.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(utcNow);
    }
}
