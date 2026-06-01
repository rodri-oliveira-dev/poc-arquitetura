using System.Diagnostics;
using System.Text.Json;

using BalanceService.Application.Balances.Commands;
using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;
using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.Contracts;
using BalanceService.Worker.Messaging.Processors;
using BalanceService.Worker.Observability;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace BalanceService.Worker.Tests.Messaging.Processors;

public sealed class LedgerEntryCreatedMessageProcessorTests
{
    [Fact]
    public async Task Invalid_json_should_publish_to_dlq_and_allow_commit()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sut = CreateSut(dlq);
        var message = CreateMessage("{invalid-json", AttributesWith(EventId: "evt-1", TraceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00", Baggage: "tenant=poc"));

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Equal("{invalid-json", dlq.Messages[0].OriginalPayload);
        Assert.Equal("ledger.ledgerentry.created", dlq.Messages[0].Source);
        Assert.Equal("ledger.ledgerentry.created", dlq.Messages[0].TransportMetadata["topic"]);
        Assert.Equal("0", dlq.Messages[0].TransportMetadata["partition"]);
        Assert.Equal("42", dlq.Messages[0].TransportMetadata["offset"]);
        Assert.Equal("key", dlq.Messages[0].TransportMetadata["key"]);
        Assert.Equal("Deserialization failed.", dlq.Messages[0].Reason);
        Assert.Equal(nameof(JsonException), dlq.Messages[0].ExceptionType);
        Assert.Equal(LedgerEntryCreatedV1Contract.EventType, dlq.Messages[0].EventType);
        Assert.Equal(LedgerEntryCreatedV1Contract.EventType, dlq.Messages[0].Attributes[MessageAttributeNames.EventType]);
        Assert.Equal("evt-1", dlq.Messages[0].Attributes[MessageAttributeNames.EventId]);
        Assert.False(string.IsNullOrWhiteSpace(dlq.Messages[0].Attributes[MessageAttributeNames.TraceParent]));
        Assert.Equal("tenant=poc", dlq.Messages[0].Attributes[MessageAttributeNames.Baggage]);
    }

    [Fact]
    public async Task Missing_event_type_should_publish_to_dlq()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sut = CreateSut(dlq);
        var message = CreateMessage(ValidPayload(), new Dictionary<string, string>());

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Equal("Missing required message attribute event_type.", dlq.Messages[0].Reason);
    }

    [Fact]
    public async Task Formal_example_should_be_consumed()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sut = CreateSut(dlq);
        var payload = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "docs",
            "contracts",
            "events",
            "LedgerEntryCreated.v1.example.json"));
        var message = CreateMessage(payload, AttributesWith(EventId: "evt-example"));

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);

        Assert.True(shouldCommit);
        Assert.Empty(dlq.Messages);
    }

    [Fact]
    public async Task Unexpected_currency_should_publish_to_dlq()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sut = CreateSut(dlq);
        var payload = ValidPayload().Replace("\"externalReference\":null", "\"externalReference\":null,\"currency\":\"BRL\"");
        var message = CreateMessage(payload, AttributesWith(EventId: "evt-currency"));

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);

        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Equal("Deserialization failed.", dlq.Messages[0].Reason);
        Assert.Equal(nameof(JsonException), dlq.Messages[0].ExceptionType);
    }

    [Fact]
    public async Task Missing_occurred_at_should_publish_to_dlq()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sut = CreateSut(dlq);
        var payload = ValidPayload().Replace("\"occurredAt\":\"2026-02-16T00:00:00.0000000Z\",", string.Empty);
        var message = CreateMessage(payload, AttributesWith(EventId: "evt-missing-occurred-at"));

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);

        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Equal("Message payload occurredAt is required.", dlq.Messages[0].Reason);
    }

    [Fact]
    public async Task Estorno_request_event_should_not_be_processed_as_financial_event()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(dlq, sender.Object);
        var attributes = new Dictionary<string, string>
        {
            [MessageAttributeNames.EventType] = "LancamentoEstornoSolicitado.v1",
            [MessageAttributeNames.EventId] = "evt-estorno-1"
        };

        var message = CreateMessage(JsonSerializer.Serialize(new
        {
            estornoId = Guid.NewGuid(),
            lancamentoOriginalId = Guid.NewGuid(),
            merchantId = "m1",
            motivo = "Erro operacional",
            status = "Pending",
            requestedAt = "2026-05-06T10:00:00.0000000Z",
            correlationId = Guid.NewGuid().ToString()
        }), attributes);

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Contains("Unsupported message event_type", dlq.Messages[0].Reason);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reprocessamento_request_event_should_not_be_processed_as_financial_event()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(dlq, sender.Object);
        var attributes = new Dictionary<string, string>
        {
            [MessageAttributeNames.EventType] = "ReprocessamentoLancamentosSolicitado.v1",
            [MessageAttributeNames.EventId] = "evt-reprocessamento-1"
        };

        var message = CreateMessage(JsonSerializer.Serialize(new
        {
            reprocessamentoId = Guid.NewGuid(),
            merchantId = "m1",
            dataInicial = "2026-05-01",
            dataFinal = "2026-05-06",
            motivo = "Correcao de regra de consolidacao",
            status = "Pending",
            requestedAt = "2026-05-07T10:00:00.0000000",
            correlationId = Guid.NewGuid().ToString()
        }), attributes);

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Contains("Unsupported message event_type", dlq.Messages[0].Reason);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Missing_event_id_should_process_using_payload_id()
    {
        var dlq = new CapturingDeadLetterProducer();
        ApplyLedgerEntryCreatedCommand? command = null;
        var sender = new Mock<ISender>();
        sender
            .Setup(x => x.Send(It.IsAny<ApplyLedgerEntryCreatedCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ApplyLedgerEntryCreatedResult>, CancellationToken>((request, _) => command = request as ApplyLedgerEntryCreatedCommand)
            .ReturnsAsync(ApplyLedgerEntryCreatedResult.Processed);

        var sut = CreateSut(dlq, sender.Object);
        var message = CreateMessage(ValidPayload(), AttributesWith(EventId: null));

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Empty(dlq.Messages);
        Assert.NotNull(command);
        Assert.Equal("lan_12345678", command!.Event.Id);
    }

    [Fact]
    public async Task Valid_message_should_restore_trace_context_and_baggage()
    {
        Activity? stoppedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == LedgerEntryCreatedMessageProcessor.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stoppedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        var dlq = new CapturingDeadLetterProducer();
        var sender = new Mock<ISender>();
        sender
            .Setup(x => x.Send(It.IsAny<ApplyLedgerEntryCreatedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplyLedgerEntryCreatedResult.Processed);

        var sut = CreateSut(dlq, sender.Object);
        var message = CreateMessage(
            ValidPayload(),
            AttributesWith(
                EventId: "evt-1",
                TraceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
                TraceState: "vendor=value",
                Baggage: "tenant=poc"));

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.NotNull(stoppedActivity);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", stoppedActivity!.TraceId.ToString());
        Assert.Equal("00f067aa0ba902b7", stoppedActivity.ParentSpanId.ToString());
        Assert.Contains(stoppedActivity.Baggage, x => x.Key == "tenant" && x.Value == "poc");
    }

    [Fact]
    public async Task Invalid_amount_should_publish_to_dlq()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sut = CreateSut(dlq);
        var message = CreateMessage(ValidPayload(amount: "not-a-decimal"), AttributesWith(EventId: "evt-1"));

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Equal("Message payload type and amount are invalid.", dlq.Messages[0].Reason);
        Assert.Equal("MessageValidationException", dlq.Messages[0].ExceptionType);
    }

    [Fact]
    public async Task Non_recoverable_processing_failure_should_publish_to_dlq()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sender = new Mock<ISender>();
        sender
            .Setup(x => x.Send(It.IsAny<ApplyLedgerEntryCreatedCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Invalid amount format."));

        var sut = CreateSut(dlq, sender.Object);
        var message = CreateMessage(ValidPayload(), AttributesWith(EventId: "evt-1"));

        var shouldCommit = await sut.ProcessAsync(message, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Equal("Non-recoverable processing failure.", dlq.Messages[0].Reason);
        Assert.Equal(nameof(DomainException), dlq.Messages[0].ExceptionType);
    }

    [Fact]
    public async Task Dlq_publish_failure_should_not_allow_commit()
    {
        var dlq = new CapturingDeadLetterProducer { ThrowOnProduce = true };
        var sut = CreateSut(dlq);
        var message = CreateMessage("{invalid-json", AttributesWith(EventId: "evt-1"));

        var act = async () => await sut.ProcessAsync(message, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("DLQ unavailable.").Replace("\\*", ".*") + "$", ex.Message);
    }

    private static LedgerEntryCreatedMessageProcessor CreateSut(
        IDeadLetterPublisher dlq,
        ISender? sender = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sender ?? CreateDefaultSender());

#pragma warning disable CA2000
        return new LedgerEntryCreatedMessageProcessor(
            services.BuildServiceProvider(),
            dlq,
            new KafkaMessagingMetrics($"{KafkaMessagingMetrics.MeterName}.Tests.{Guid.NewGuid():N}"),
            NullLogger<LedgerEntryCreatedMessageProcessor>.Instance);
#pragma warning restore CA2000
    }

    private static ISender CreateDefaultSender()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(x => x.Send(It.IsAny<ApplyLedgerEntryCreatedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplyLedgerEntryCreatedResult.Processed);

        return sender.Object;
    }

    private static ReceivedMessage CreateMessage(string payload, Dictionary<string, string> attributes)
        => new(
            payload,
            attributes.TryGetValue(MessageAttributeNames.EventType, out var eventType) ? eventType : string.Empty,
            attributes.TryGetValue(MessageAttributeNames.EventId, out var eventId) ? eventId : null,
            attributes.TryGetValue(MessageAttributeNames.CorrelationId, out var correlationId) ? correlationId : null,
            attributes.TryGetValue(MessageAttributeNames.TraceParent, out var traceParent) ? traceParent : null,
            attributes.TryGetValue(MessageAttributeNames.TraceState, out var traceState) ? traceState : null,
            attributes.TryGetValue(MessageAttributeNames.Baggage, out var baggage) ? baggage : null,
            "key",
            attributes,
            new TransportMessageContext(
                "kafka",
                "ledger.ledgerentry.created",
                "0",
                "42",
                null,
                new Dictionary<string, string>
                {
                    ["topic"] = "ledger.ledgerentry.created",
                    ["partition"] = "0",
                    ["offset"] = "42",
                    ["key"] = "key"
                }));

    private static Dictionary<string, string> AttributesWith(
        string? EventId,
        string? TraceParent = null,
        string? TraceState = null,
        string? Baggage = null)
    {
        var attributes = new Dictionary<string, string>
        {
            [MessageAttributeNames.EventType] = LedgerEntryCreatedV1Contract.EventType
        };

        if (EventId is not null)
            attributes[MessageAttributeNames.EventId] = EventId;

        if (TraceParent is not null)
            attributes[MessageAttributeNames.TraceParent] = TraceParent;

        if (TraceState is not null)
            attributes[MessageAttributeNames.TraceState] = TraceState;

        if (Baggage is not null)
            attributes[MessageAttributeNames.Baggage] = Baggage;

        return attributes;
    }

    private static string ValidPayload(string amount = "10.00")
        => JsonSerializer.Serialize(new
        {
            id = "lan_12345678",
            type = "CREDIT",
            amount,
            createdAt = "2026-02-16T00:00:00.0000000Z",
            merchantId = "m1",
            occurredAt = "2026-02-16T00:00:00.0000000Z",
            description = (string?)null,
            correlationId = Guid.NewGuid().ToString(),
            externalReference = (string?)null
        });

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

    private sealed class CapturingDeadLetterProducer : IDeadLetterPublisher
    {
        public List<DeadLetterMessage> Messages { get; } = new();
        public bool ThrowOnProduce { get; init; }

        public Task PublishAsync(DeadLetterMessage message, CancellationToken cancellationToken)
        {
            if (ThrowOnProduce)
                throw new InvalidOperationException("DLQ unavailable.");

            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
