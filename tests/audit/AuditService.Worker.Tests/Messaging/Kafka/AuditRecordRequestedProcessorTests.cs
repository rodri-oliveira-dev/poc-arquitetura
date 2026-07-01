using System.Globalization;
using System.Text.Json;

using AuditService.Application.FunctionalAuditing.CreateAuditRecord;
using AuditService.Worker.Messaging.Kafka;

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
        var processor = CreateProcessor(sender);

        bool processed = await processor.ProcessAsync(ValidEventJson(), TestContext.Current.CancellationToken);

        Assert.True(processed);
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
    public async Task ProcessAsync_should_treat_invalid_contract_as_definitive_error()
    {
        var sender = new RecordingSender();
        var processor = CreateProcessor(sender);

        bool processed = await processor.ProcessAsync(
            ValidEventJson(new Dictionary<string, object?>
            {
                ["eventType"] = "OtherEvent.v1"
            }),
            TestContext.Current.CancellationToken);

        Assert.True(processed);
        Assert.Null(sender.Request);
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
            processor.ProcessAsync(ValidEventJson(), TestContext.Current.CancellationToken));
    }

    private static AuditRecordRequestedProcessor CreateProcessor(RecordingSender sender)
        => new(
            new AuditRecordRequestedValidator(),
            sender,
            NullLogger<AuditRecordRequestedProcessor>.Instance);

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

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
                throw Exception;

            Request = request;
            object result = new CreateAuditRecordResult(Guid.NewGuid());
            return Task.FromResult((TResponse)result);
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
}
