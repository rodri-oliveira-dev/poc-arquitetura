using System.Text;
using System.Text.Json;
using System.Diagnostics;

using BalanceService.Application.Balances.Commands;
using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;
using BalanceService.Worker.Messaging.Kafka.Contracts;
using BalanceService.Worker.Messaging.Kafka.DeadLetter;
using BalanceService.Worker.Messaging.Kafka.Processors;
using BalanceService.Worker.Messaging.Kafka.Tracing;
using BalanceService.Worker.Observability;

using Confluent.Kafka;


using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace BalanceService.Worker.Tests.Messaging.Kafka.Processors;

public sealed class LedgerKafkaMessageProcessorTests
{
    [Fact]
    public async Task Invalid_json_should_publish_to_dlq_and_allow_commit()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sut = CreateSut(dlq);
        var result = CreateResult("{invalid-json", HeadersWith(EventId: "evt-1", TraceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00", Baggage: "tenant=poc"));

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Equal("{invalid-json", dlq.Messages[0].OriginalPayload);
        Assert.Equal("ledger.ledgerentry.created", dlq.Messages[0].OriginalTopic);
        Assert.Equal(0, dlq.Messages[0].OriginalPartition);
        Assert.Equal(42, dlq.Messages[0].OriginalOffset);
        Assert.Equal("Deserialization failed.", dlq.Messages[0].Reason);
        Assert.Equal(nameof(JsonException), dlq.Messages[0].ExceptionType);
        Assert.Equal(LedgerEntryCreatedV1Contract.EventType, dlq.Messages[0].OriginalHeaders[KafkaHeaderNames.EventType]);
        Assert.Equal("evt-1", dlq.Messages[0].OriginalHeaders[KafkaHeaderNames.EventId]);
        Assert.False(string.IsNullOrWhiteSpace(dlq.Messages[0].OriginalHeaders[KafkaHeaderNames.TraceParent]));
        Assert.Equal("tenant=poc", dlq.Messages[0].OriginalHeaders[KafkaHeaderNames.Baggage]);
    }

    [Fact]
    public async Task Missing_event_type_should_publish_to_dlq()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sut = CreateSut(dlq);
        var result = CreateResult(ValidPayload(), new Headers());

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Equal("Missing required Kafka header event_type.", dlq.Messages[0].Reason);
    }

    [Fact]
    public async Task Estorno_request_event_should_not_be_processed_as_financial_event()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(dlq, sender.Object);
        var headers = new Headers
        {
            { KafkaHeaderNames.EventType, Encoding.UTF8.GetBytes("LancamentoEstornoSolicitado.v1") },
            { KafkaHeaderNames.EventId, Encoding.UTF8.GetBytes("evt-estorno-1") }
        };

        var result = CreateResult(JsonSerializer.Serialize(new
        {
            estornoId = Guid.NewGuid(),
            lancamentoOriginalId = Guid.NewGuid(),
            merchantId = "m1",
            motivo = "Erro operacional",
            status = "Pending",
            requestedAt = "2026-05-06T10:00:00.0000000Z",
            correlationId = Guid.NewGuid().ToString()
        }), headers);

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Contains("Unsupported Kafka event_type", dlq.Messages[0].Reason);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reprocessamento_request_event_should_not_be_processed_as_financial_event()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var sut = CreateSut(dlq, sender.Object);
        var headers = new Headers
        {
            { KafkaHeaderNames.EventType, Encoding.UTF8.GetBytes("ReprocessamentoLancamentosSolicitado.v1") },
            { KafkaHeaderNames.EventId, Encoding.UTF8.GetBytes("evt-reprocessamento-1") }
        };

        var result = CreateResult(JsonSerializer.Serialize(new
        {
            reprocessamentoId = Guid.NewGuid(),
            merchantId = "m1",
            dataInicial = "2026-05-01",
            dataFinal = "2026-05-06",
            motivo = "Correcao de regra de consolidacao",
            status = "Pending",
            requestedAt = "2026-05-07T10:00:00.0000000",
            correlationId = Guid.NewGuid().ToString()
        }), headers);

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.Single(dlq.Messages);
        Assert.Contains("Unsupported Kafka event_type", dlq.Messages[0].Reason);
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
        var result = CreateResult(ValidPayload(), HeadersWith(EventId: null));

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);
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
            ShouldListenTo = source => source.Name == "BalanceService.KafkaConsumer",
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
        var result = CreateResult(
            ValidPayload(),
            HeadersWith(
                EventId: "evt-1",
                TraceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
                TraceState: "vendor=value",
                Baggage: "tenant=poc"));

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);
        Assert.True(shouldCommit);
        Assert.NotNull(stoppedActivity);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", stoppedActivity!.TraceId.ToString());
        Assert.Equal("00f067aa0ba902b7", stoppedActivity.ParentSpanId.ToString());
        Assert.Contains(stoppedActivity.Baggage, x => x.Key == "tenant" && x.Value == "poc");
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
        var result = CreateResult(ValidPayload(amount: "not-a-decimal"), HeadersWith(EventId: "evt-1"));

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);
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
        var result = CreateResult("{invalid-json", HeadersWith(EventId: "evt-1"));

        var act = async () => await sut.ProcessAsync(result, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("DLQ unavailable.").Replace("\\*", ".*") + "$", ex.Message);
    }

    private static LedgerKafkaMessageProcessor CreateSut(
        IKafkaDeadLetterProducer dlq,
        ISender? sender = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sender ?? CreateDefaultSender());

#pragma warning disable CA2000
        return new LedgerKafkaMessageProcessor(
            services.BuildServiceProvider(),
            dlq,
            new KafkaMessagingMetrics($"{KafkaMessagingMetrics.MeterName}.Tests.{Guid.NewGuid():N}"),
            NullLogger<LedgerKafkaMessageProcessor>.Instance);
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

    private static ConsumeResult<string, string> CreateResult(string payload, Headers headers)
        => new()
        {
            Topic = "ledger.ledgerentry.created",
            Partition = new Partition(0),
            Offset = new Offset(42),
            Message = new Message<string, string>
            {
                Key = "key",
                Value = payload,
                Headers = headers
            }
        };

    private static Headers HeadersWith(
        string? EventId,
        string? TraceParent = null,
        string? TraceState = null,
        string? Baggage = null)
    {
        var headers = new Headers
        {
            { KafkaHeaderNames.EventType, Encoding.UTF8.GetBytes(LedgerEntryCreatedV1Contract.EventType) }
        };

        if (EventId is not null)
            headers.Add(KafkaHeaderNames.EventId, Encoding.UTF8.GetBytes(EventId));

        if (TraceParent is not null)
            headers.Add(KafkaHeaderNames.TraceParent, Encoding.UTF8.GetBytes(TraceParent));

        if (TraceState is not null)
            headers.Add(KafkaHeaderNames.TraceState, Encoding.UTF8.GetBytes(TraceState));

        if (Baggage is not null)
            headers.Add(KafkaHeaderNames.Baggage, Encoding.UTF8.GetBytes(Baggage));

        return headers;
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

    private sealed class CapturingDeadLetterProducer : IKafkaDeadLetterProducer
    {
        public List<DeadLetterMessage> Messages { get; } = new();
        public bool ThrowOnProduce { get; init; }

        public Task ProduceAsync(DeadLetterMessage message, CancellationToken cancellationToken)
        {
            if (ThrowOnProduce)
                throw new InvalidOperationException("DLQ unavailable.");

            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
