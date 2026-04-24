using System.Text;
using System.Text.Json;

using BalanceService.Application.Balances.Commands;
using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;
using BalanceService.Infrastructure.Messaging.Kafka;

using Confluent.Kafka;

using FluentAssertions;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace BalanceService.UnitTests.Tests;

public sealed class LedgerKafkaMessageProcessorTests
{
    [Fact]
    public async Task Invalid_json_should_publish_to_dlq_and_allow_commit()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sut = CreateSut(dlq);
        var result = CreateResult("{invalid-json", HeadersWith(EventId: "evt-1", TraceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00", Baggage: "tenant=poc"));

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);

        shouldCommit.Should().BeTrue();
        dlq.Messages.Should().ContainSingle();
        dlq.Messages[0].OriginalPayload.Should().Be("{invalid-json");
        dlq.Messages[0].OriginalTopic.Should().Be("ledger.ledgerentry.created");
        dlq.Messages[0].OriginalPartition.Should().Be(0);
        dlq.Messages[0].OriginalOffset.Should().Be(42);
        dlq.Messages[0].Reason.Should().Be("Deserialization failed.");
        dlq.Messages[0].ExceptionType.Should().Be(nameof(JsonException));
        dlq.Messages[0].OriginalHeaders[KafkaHeaderNames.EventType].Should().Be(LedgerEntryCreatedV1Contract.EventType);
        dlq.Messages[0].OriginalHeaders[KafkaHeaderNames.EventId].Should().Be("evt-1");
        dlq.Messages[0].OriginalHeaders[KafkaHeaderNames.TraceParent].Should().NotBeNullOrWhiteSpace();
        dlq.Messages[0].OriginalHeaders[KafkaHeaderNames.Baggage].Should().Be("tenant=poc");
    }

    [Fact]
    public async Task Missing_event_type_should_publish_to_dlq()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sut = CreateSut(dlq);
        var result = CreateResult(ValidPayload(), new Headers());

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);

        shouldCommit.Should().BeTrue();
        dlq.Messages.Should().ContainSingle();
        dlq.Messages[0].Reason.Should().Be("Missing required Kafka header event_type.");
    }

    [Fact]
    public async Task Missing_event_id_should_process_using_payload_id()
    {
        var dlq = new CapturingDeadLetterProducer();
        ApplyLedgerEntryCreatedCommand? command = null;
        var sender = new Mock<ISender>();
        sender
            .Setup(x => x.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest, CancellationToken>((request, _) => command = request as ApplyLedgerEntryCreatedCommand)
            .Returns(Task.CompletedTask);

        var sut = CreateSut(dlq, sender.Object);
        var result = CreateResult(ValidPayload(), HeadersWith(EventId: null));

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);

        shouldCommit.Should().BeTrue();
        dlq.Messages.Should().BeEmpty();
        command.Should().NotBeNull();
        command!.Event.Id.Should().Be("lan_12345678");
    }

    [Fact]
    public async Task Non_recoverable_processing_failure_should_publish_to_dlq()
    {
        var dlq = new CapturingDeadLetterProducer();
        var sender = new Mock<ISender>();
        sender
            .Setup(x => x.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Invalid amount format."));

        var sut = CreateSut(dlq, sender.Object);
        var result = CreateResult(ValidPayload(amount: "not-a-decimal"), HeadersWith(EventId: "evt-1"));

        var shouldCommit = await sut.ProcessAsync(result, CancellationToken.None);

        shouldCommit.Should().BeTrue();
        dlq.Messages.Should().ContainSingle();
        dlq.Messages[0].Reason.Should().Be("Non-recoverable processing failure.");
        dlq.Messages[0].ExceptionType.Should().Be(nameof(DomainException));
    }

    [Fact]
    public async Task Dlq_publish_failure_should_not_allow_commit()
    {
        var dlq = new CapturingDeadLetterProducer { ThrowOnProduce = true };
        var sut = CreateSut(dlq);
        var result = CreateResult("{invalid-json", HeadersWith(EventId: "evt-1"));

        var act = async () => await sut.ProcessAsync(result, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("DLQ unavailable.");
    }

    private static LedgerKafkaMessageProcessor CreateSut(
        IKafkaDeadLetterProducer dlq,
        ISender? sender = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sender ?? Mock.Of<ISender>());

        return new LedgerKafkaMessageProcessor(
            services.BuildServiceProvider(),
            dlq,
            NullLogger<LedgerKafkaMessageProcessor>.Instance);
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

    private static Headers HeadersWith(string? EventId, string? TraceParent = null, string? Baggage = null)
    {
        var headers = new Headers
        {
            { KafkaHeaderNames.EventType, Encoding.UTF8.GetBytes(LedgerEntryCreatedV1Contract.EventType) }
        };

        if (EventId is not null)
            headers.Add(KafkaHeaderNames.EventId, Encoding.UTF8.GetBytes(EventId));

        if (TraceParent is not null)
            headers.Add(KafkaHeaderNames.TraceParent, Encoding.UTF8.GetBytes(TraceParent));

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
