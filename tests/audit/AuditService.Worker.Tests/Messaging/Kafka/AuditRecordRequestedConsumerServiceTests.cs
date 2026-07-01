using AuditService.Worker.Messaging.Kafka;
using AuditService.Worker.Messaging.Kafka.Configuration;
using AuditService.Worker.Observability;

using Confluent.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace AuditService.Worker.Tests.Messaging.Kafka;

public sealed class AuditRecordRequestedConsumerServiceTests
{
    private static readonly AuditWorkerMetrics Metrics = new($"AuditService.Worker.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task ConsumeOnceAsync_should_commit_after_processor_success()
    {
        var processor = new FakeProcessor(AuditRecordRequestedProcessingResult.Success);
        using var service = CreateService(processor);
        using var consumer = new FakeConsumer("message");

        bool consumed = await service.ConsumeOnceAsync(consumer, TestContext.Current.CancellationToken);

        Assert.True(consumed);
        Assert.Equal("message", processor.Messages.Single().Payload);
        Assert.Equal(1, consumer.CommitCalls);
    }

    [Fact]
    public async Task ConsumeOnceAsync_should_not_commit_when_processor_does_not_complete()
    {
        var processor = new FakeProcessor(AuditRecordRequestedProcessingResult.NotProcessed);
        using var service = CreateService(processor);
        using var consumer = new FakeConsumer("message");

        bool consumed = await service.ConsumeOnceAsync(consumer, TestContext.Current.CancellationToken);

        Assert.False(consumed);
        Assert.Empty(consumer.CommittedOffsets);
    }

    [Fact]
    public async Task ConsumeOnceAsync_should_not_commit_when_processor_throws()
    {
        var processor = new FakeProcessor(AuditRecordRequestedProcessingResult.Success)
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
    public async Task ConsumeOnceAsync_should_retry_transient_failure_before_commit()
    {
        var processor = new FakeProcessor(AuditRecordRequestedProcessingResult.Success)
        {
            RemainingFailures = 1
        };
        using var service = CreateService(processor);
        using var consumer = new FakeConsumer("message");

        bool consumed = await service.ConsumeOnceAsync(consumer, TestContext.Current.CancellationToken);

        Assert.True(consumed);
        Assert.Equal(2, processor.Attempts);
        Assert.Equal(1, consumer.CommitCalls);
    }

    [Fact]
    public async Task ConsumeOnceAsync_should_respect_cancellation_token_during_retry_delay()
    {
        var processor = new FakeProcessor(AuditRecordRequestedProcessingResult.Success)
        {
            Exception = new InvalidOperationException("database unavailable")
        };
        using var service = CreateService(processor, processingRetryDelay: TimeSpan.FromSeconds(5));
        using var consumer = new FakeConsumer("message");
        using var cts = new CancellationTokenSource();

        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ConsumeOnceAsync(consumer, cts.Token));

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

    [Fact]
    public void ValidateOptions_should_skip_required_values_when_consumer_is_disabled()
    {
        var options = new AuditRecordRequestedConsumerOptions
        {
            Enabled = false,
            BootstrapServers = "",
            GroupId = "",
            Topic = "",
            DeadLetterTopic = "",
            DeadLetterMessageTimeoutMs = 0,
            MaxProcessingAttempts = 0,
            ProcessingRetryDelay = TimeSpan.Zero,
            ConsumeErrorRetryDelay = TimeSpan.Zero,
            ProcessingErrorRetryDelay = TimeSpan.Zero
        };

        AuditRecordRequestedConsumerService.ValidateOptions(options);
    }

    [Theory]
    [InlineData("GroupId")]
    [InlineData("Topic")]
    [InlineData("DeadLetterTopic")]
    [InlineData("DeadLetterMessageTimeoutMs")]
    [InlineData("MaxProcessingAttempts")]
    [InlineData("ProcessingRetryDelay")]
    [InlineData("ConsumeErrorRetryDelay")]
    [InlineData("ProcessingErrorRetryDelay")]
    public void ValidateOptions_should_reject_invalid_enabled_consumer_options(
        string expectedMessage)
    {
        AuditRecordRequestedConsumerOptions options = InvalidOptions(expectedMessage);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            AuditRecordRequestedConsumerService.ValidateOptions(options));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    private static AuditRecordRequestedConsumerOptions InvalidOptions(string optionName)
    {
        return optionName switch
        {
            "GroupId" => ValidOptions(options => options.GroupId = ""),
            "Topic" => ValidOptions(options => options.Topic = ""),
            "DeadLetterTopic" => ValidOptions(options => options.DeadLetterTopic = ""),
            "DeadLetterMessageTimeoutMs" => ValidOptions(options => options.DeadLetterMessageTimeoutMs = 0),
            "MaxProcessingAttempts" => ValidOptions(options => options.MaxProcessingAttempts = 0),
            "ProcessingRetryDelay" => ValidOptions(options => options.ProcessingRetryDelay = TimeSpan.Zero),
            "ConsumeErrorRetryDelay" => ValidOptions(options => options.ConsumeErrorRetryDelay = TimeSpan.Zero),
            "ProcessingErrorRetryDelay" => ValidOptions(options => options.ProcessingErrorRetryDelay = TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(optionName), optionName, "Invalid option name.")
        };
    }

    private static AuditRecordRequestedConsumerOptions ValidOptions(
        Action<MutableAuditRecordRequestedConsumerOptions>? configure = null)
    {
        var options = new MutableAuditRecordRequestedConsumerOptions
        {
            Enabled = true,
            BootstrapServers = "localhost:9092",
            GroupId = "audit-group",
            Topic = "audit.record.requested",
            DeadLetterTopic = "audit.record.requested.dlq",
            DeadLetterMessageTimeoutMs = 1000,
            MaxProcessingAttempts = 3,
            ProcessingRetryDelay = TimeSpan.FromMilliseconds(1),
            ConsumeErrorRetryDelay = TimeSpan.FromMilliseconds(1),
            ProcessingErrorRetryDelay = TimeSpan.FromMilliseconds(1)
        };

        configure?.Invoke(options);

        return options.ToOptions();
    }

    private sealed class MutableAuditRecordRequestedConsumerOptions
    {
        public bool Enabled
        {
            get; init;
        }
        public string BootstrapServers { get; init; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string DeadLetterTopic { get; set; } = string.Empty;
        public int DeadLetterMessageTimeoutMs
        {
            get; set;
        }
        public int MaxProcessingAttempts
        {
            get; set;
        }
        public TimeSpan ProcessingRetryDelay
        {
            get; set;
        }
        public TimeSpan ConsumeErrorRetryDelay
        {
            get; set;
        }
        public TimeSpan ProcessingErrorRetryDelay
        {
            get; set;
        }

        public AuditRecordRequestedConsumerOptions ToOptions()
            => new()
            {
                Enabled = Enabled,
                BootstrapServers = BootstrapServers,
                GroupId = GroupId,
                Topic = Topic,
                DeadLetterTopic = DeadLetterTopic,
                DeadLetterMessageTimeoutMs = DeadLetterMessageTimeoutMs,
                MaxProcessingAttempts = MaxProcessingAttempts,
                ProcessingRetryDelay = ProcessingRetryDelay,
                ConsumeErrorRetryDelay = ConsumeErrorRetryDelay,
                ProcessingErrorRetryDelay = ProcessingErrorRetryDelay
            };
    }

    private static AuditRecordRequestedConsumerService CreateService(
        FakeProcessor processor,
        TimeSpan? processingRetryDelay = null)
        => new(
            Microsoft.Extensions.Options.Options.Create(new AuditRecordRequestedConsumerOptions
            {
                Enabled = true,
                BootstrapServers = "localhost:9092",
                ProcessingRetryDelay = processingRetryDelay ?? TimeSpan.FromMilliseconds(1)
            }),
            new FakeConsumerFactory(),
            processor,
            Metrics,
            NullLogger<AuditRecordRequestedConsumerService>.Instance);

    private sealed class FakeProcessor(AuditRecordRequestedProcessingResult result) : IAuditRecordRequestedProcessor
    {
        public List<AuditKafkaReceivedMessage> Messages { get; } = [];

        public int Attempts
        {
            get; private set;
        }

        public int RemainingFailures
        {
            get; init;
        }

        public Exception? Exception
        {
            get; init;
        }

        public Task<AuditRecordRequestedProcessingResult> ProcessAsync(
            AuditKafkaReceivedMessage message,
            CancellationToken cancellationToken)
        {
            Attempts++;

            if (Exception is not null)
                throw Exception;

            if (Attempts <= RemainingFailures)
                throw new InvalidOperationException("database unavailable");

            Messages.Add(message);
            return Task.FromResult(result);
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
