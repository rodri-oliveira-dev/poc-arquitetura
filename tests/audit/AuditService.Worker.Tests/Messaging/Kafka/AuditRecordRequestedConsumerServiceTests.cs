using AuditService.Worker.Messaging.Kafka;
using AuditService.Worker.Messaging.Kafka.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace AuditService.Worker.Tests.Messaging.Kafka;

public sealed class AuditRecordRequestedConsumerServiceTests
{
    [Fact]
    public async Task ConsumeOnceAsync_should_commit_after_processor_success()
    {
        var processor = new FakeProcessor(processed: true);
        using var service = CreateService(processor);
        using var consumer = new FakeConsumer("message");

        bool consumed = await service.ConsumeOnceAsync(consumer, TestContext.Current.CancellationToken);

        Assert.True(consumed);
        Assert.Equal("message", processor.Messages.Single());
        Assert.Equal(1, consumer.CommitCalls);
    }

    [Fact]
    public async Task ConsumeOnceAsync_should_not_commit_when_processor_does_not_complete()
    {
        var processor = new FakeProcessor(processed: false);
        using var service = CreateService(processor);
        using var consumer = new FakeConsumer("message");

        bool consumed = await service.ConsumeOnceAsync(consumer, TestContext.Current.CancellationToken);

        Assert.False(consumed);
        Assert.Empty(consumer.CommittedOffsets);
    }

    [Fact]
    public async Task ConsumeOnceAsync_should_not_commit_when_processor_throws()
    {
        var processor = new FakeProcessor(processed: false)
        {
            Exception = new InvalidOperationException("database unavailable")
        };
        using var service = CreateService(processor);
        using var consumer = new FakeConsumer("message");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConsumeOnceAsync(consumer, TestContext.Current.CancellationToken));

        Assert.Empty(consumer.CommittedOffsets);
    }

    [Fact]
    public void ValidateOptions_should_reject_enabled_consumer_without_bootstrap_servers()
    {
        var options = new AuditRecordRequestedConsumerOptions
        {
            Enabled = true,
            BootstrapServers = ""
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AuditRecordRequestedConsumerService.ValidateOptions(options));

        Assert.Contains("BootstrapServers", exception.Message, StringComparison.Ordinal);
    }

    private static AuditRecordRequestedConsumerService CreateService(FakeProcessor processor)
        => new(
            Microsoft.Extensions.Options.Options.Create(new AuditRecordRequestedConsumerOptions
            {
                Enabled = true,
                BootstrapServers = "localhost:9092"
            }),
            new FakeConsumerFactory(),
            processor,
            NullLogger<AuditRecordRequestedConsumerService>.Instance);

    private sealed class FakeProcessor(bool processed) : IAuditRecordRequestedProcessor
    {
        public List<string> Messages { get; } = [];

        public Exception? Exception
        {
            get; init;
        }

        public Task<bool> ProcessAsync(string messageValue, CancellationToken cancellationToken)
        {
            if (Exception is not null)
                throw Exception;

            Messages.Add(messageValue);
            return Task.FromResult(processed);
        }
    }

    private sealed class FakeConsumerFactory : IAuditKafkaConsumerFactory
    {
        public IAuditKafkaConsumer Create()
            => throw new NotSupportedException();
    }

    private sealed class FakeConsumer(string value) : IAuditKafkaConsumer
    {
        private bool _consumed;

        public List<Offset> CommittedOffsets { get; } = [];

        public int CommitCalls => CommittedOffsets.Count;

        public void Subscribe(string topic)
        {
        }

        public ConsumeResult<string, string>? Consume(CancellationToken cancellationToken)
        {
            if (_consumed)
                return null;

            _consumed = true;
            return new ConsumeResult<string, string>
            {
                Topic = "audit.record.requested",
                Partition = new Partition(0),
                Offset = new Offset(42),
                Message = new Message<string, string>
                {
                    Key = "key",
                    Value = value
                }
            };
        }

        public void Commit(ConsumeResult<string, string> result)
            => CommittedOffsets.Add(result.Offset);

        public void Close()
        {
        }

        public void Dispose()
        {
        }
    }
}
