using System.Globalization;
using System.Text.Json;

using AuditService.Application.FunctionalAuditing.CreateAuditRecord;
using AuditService.Worker.Messaging.Kafka;
using AuditService.Worker.Messaging.Kafka.DeadLetter;

using MediatR;

using Microsoft.Extensions.Logging.Abstractions;

namespace AuditService.Worker.Tests.Messaging.Kafka;

public sealed class AuditRecordRequestedProcessorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProcessAsync_should_map_valid_event_to_create_command_with_source_event_id()
    {
        var sender = new RecordingSender();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var processor = CreateProcessor(sender, deadLetterPublisher);

        AuditRecordRequestedProcessingResult processed = await processor.ProcessAsync(ReceivedMessage(ValidEventJson()), TestContext.Current.CancellationToken);

        Assert.True(processed.ShouldCommit);
        Assert.Equal("success", processed.Result);
        Assert.Empty(deadLetterPublisher.Messages);
        CreateAuditRecordCommand command = Assert.IsType<CreateAuditRecordCommand>(sender.Request);
        Assert.Null(command.IdempotencyKey);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), command.SourceEventId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), command.OperationId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000003"), command.CorrelationId);
        Assert.Equal("LedgerService", command.SourceService);
        Assert.Equal("LancamentoCriado", command.OperationType);
        Assert.Equal("100.00", command.Metadata!["amount"]);
    }

    [Fact]
    public async Task ProcessAsync_should_report_duplicate_event_as_idempotent()
    {
        var sender = new RecordingSender
        {
            Result = new CreateAuditRecordResult(Guid.Parse("00000000-0000-0000-0000-000000000010"), Duplicate: true)
        };
        var processor = CreateProcessor(sender);

        AuditRecordRequestedProcessingResult processed = await processor.ProcessAsync(
            ReceivedMessage(ValidEventJson()),
            TestContext.Current.CancellationToken);

        Assert.True(processed.ShouldCommit);
        Assert.Equal("duplicate", processed.Result);
    }

    [Fact]
    public async Task ProcessAsync_should_send_invalid_contract_to_dlq()
    {
        var sender = new RecordingSender();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var processor = CreateProcessor(sender, deadLetterPublisher);

        AuditRecordRequestedProcessingResult processed = await processor.ProcessAsync(
            ReceivedMessage(
            ValidEventJson(new Dictionary<string, object?>
            {
                ["eventType"] = "OtherEvent.v1"
            })),
            TestContext.Current.CancellationToken);

        Assert.True(processed.ShouldCommit);
        Assert.Equal("dlq", processed.Result);
        Assert.Null(sender.Request);
        AuditRecordDeadLetterMessage deadLetter = Assert.Single(deadLetterPublisher.Messages);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), deadLetter.EventId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000003"), deadLetter.CorrelationId);
        Assert.Equal("invalid_contract", deadLetter.FailureCategory);
        Assert.Equal("audit.record.requested", deadLetter.OriginalTopic);
        Assert.Equal(0, deadLetter.OriginalPartition);
        Assert.Equal(42, deadLetter.OriginalOffset);
        Assert.False(string.IsNullOrWhiteSpace(deadLetter.PayloadSha256));
    }

    [Fact]
    public async Task ProcessAsync_should_send_invalid_json_to_dlq_without_original_payload()
    {
        var sender = new RecordingSender();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var processor = CreateProcessor(sender, deadLetterPublisher);

        AuditRecordRequestedProcessingResult processed = await processor.ProcessAsync(
            ReceivedMessage("{ invalid"),
            TestContext.Current.CancellationToken);

        Assert.True(processed.ShouldCommit);
        Assert.Equal("dlq", processed.Result);
        Assert.Null(sender.Request);
        AuditRecordDeadLetterMessage deadLetter = Assert.Single(deadLetterPublisher.Messages);
        Assert.Null(deadLetter.EventId);
        Assert.Null(deadLetter.CorrelationId);
        Assert.Equal("invalid_json", deadLetter.FailureCategory);
        Assert.False(deadLetter.PayloadSha256.Contains("{ invalid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessAsync_should_send_idempotency_conflict_to_dlq()
    {
        var sender = new RecordingSender
        {
            Exception = new Application.Common.Exceptions.ConflictException("SourceEventId already used with a different payload.")
        };
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var processor = CreateProcessor(sender, deadLetterPublisher);

        AuditRecordRequestedProcessingResult processed = await processor.ProcessAsync(
            ReceivedMessage(ValidEventJson()),
            TestContext.Current.CancellationToken);

        Assert.True(processed.ShouldCommit);
        Assert.Equal("dlq", processed.Result);
        Assert.Single(deadLetterPublisher.Messages);
    }

    [Fact]
    public async Task ProcessAsync_should_propagate_persistence_failures()
    {
        var sender = new RecordingSender
        {
            Exception = new InvalidOperationException("database unavailable")
        };
        var processor = CreateProcessor(sender);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(ReceivedMessage(ValidEventJson()), TestContext.Current.CancellationToken));
    }

    private static AuditRecordRequestedProcessor CreateProcessor(
        RecordingSender sender,
        RecordingDeadLetterPublisher? deadLetterPublisher = null)
        => new(
            new AuditRecordRequestedValidator(),
            deadLetterPublisher ?? new RecordingDeadLetterPublisher(),
            sender,
            NullLogger<AuditRecordRequestedProcessor>.Instance);

    private static AuditKafkaReceivedMessage ReceivedMessage(string payload)
        => new(payload, "audit.record.requested", 0, 42);

    private static string ValidEventJson(Dictionary<string, object?>? overrides = null)
    {
        var values = new Dictionary<string, object?>
        {
            ["eventId"] = "00000000-0000-0000-0000-000000000001",
            ["eventType"] = "AuditRecordRequested.v1",
            ["schemaVersion"] = 1,
            ["occurredAt"] = DateTimeOffset.Parse("2026-07-01T10:30:00Z", CultureInfo.InvariantCulture),
            ["sourceService"] = "LedgerService",
            ["operationId"] = "00000000-0000-0000-0000-000000000002",
            ["correlationId"] = "00000000-0000-0000-0000-000000000003",
            ["operationType"] = "LancamentoCriado",
            ["entityType"] = "Lancamento",
            ["entityId"] = "lan_123",
            ["merchantId"] = "m1",
            ["actor"] = new
            {
                type = "Client",
                subject = "poc-automation",
                clientId = "poc-automation"
            },
            ["status"] = "Succeeded",
            ["reason"] = null,
            ["metadata"] = new Dictionary<string, string>
            {
                ["amount"] = "100.00",
                ["currency"] = "BRL"
            }
        };

        if (overrides is not null)
        {
            foreach (KeyValuePair<string, object?> item in overrides)
            {
                values[item.Key] = item.Value;
            }
        }

        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private sealed class RecordingSender : ISender
    {
        public object? Request
        {
            get; private set;
        }

        public Exception? Exception
        {
            get; init;
        }

        public CreateAuditRecordResult Result { get; init; } = new(Guid.NewGuid());

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
                throw Exception;

            Request = request;
            return Task.FromResult((TResponse)(object)Result);
        }

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Request = request;
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult<object?>(new CreateAuditRecordResult(Guid.NewGuid()));
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<object?>();
    }

    private sealed class RecordingDeadLetterPublisher : IAuditRecordDeadLetterPublisher
    {
        public List<AuditRecordDeadLetterMessage> Messages { get; } = [];

        public Task PublishAsync(AuditRecordDeadLetterMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
