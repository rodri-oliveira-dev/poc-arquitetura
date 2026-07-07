using System.Globalization;

using BalanceService.Application;
using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Domain.Balances;
using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.Contracts;
using BalanceService.Worker.Messaging.Kafka.Consumers;
using BalanceService.Worker.Messaging.Processors;
using BalanceService.Worker.Messaging.PubSub.Consumers;
using BalanceService.Worker.Messaging.PubSub.Tracing;
using BalanceService.Worker.Observability;

using Confluent.Kafka;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ProtobufTimestamp = Google.Protobuf.WellKnownTypes.Timestamp;
using TextEncoding = System.Text.Encoding;

namespace BalanceService.Worker.Tests.Messaging.Contracts;

public sealed class LedgerEntryCreatedConsumerContractTests
{
    private const string PubSubSubscriptionId = "ledger-events-balance";
    private const string KafkaTopic = "ledger.ledgerentry.created";

    [Fact]
    public async Task PubSub_contract_fixture_should_map_metadata_deserialize_project_and_preserve_idempotency()
    {
        using var harness = new ContractHarness();
        var payload = ReadContractExample("ledger-entry-created.v2.valid.json");
        var pubSubMessage = CreatePubSubMessage(payload);

        var receivedMessage = PubSubReceivedMessageMapper.Map(pubSubMessage, PubSubSubscriptionId);

        Assert.Equal(payload, receivedMessage.Payload);
        Assert.Equal(LedgerEntryCreatedV2Contract.EventType, receivedMessage.EventType);
        Assert.Equal("evt-contract-pubsub", receivedMessage.EventId);
        Assert.Equal("2cbdd495-586f-4565-a807-c5dc6710d237", receivedMessage.CorrelationId);
        Assert.Equal("merchant-001", receivedMessage.OrderingKey);
        Assert.Equal("pubsub", receivedMessage.Transport.Provider);
        Assert.Equal(PubSubSubscriptionId, receivedMessage.Transport.Source);
        Assert.Equal("message-contract-1", receivedMessage.Transport.Metadata["message_id"]);
        Assert.Equal("2", receivedMessage.Transport.DeliveryAttempt);

        var firstCommit = await harness.Processor.ProcessAsync(receivedMessage, CancellationToken.None);
        var secondCommit = await harness.Processor.ProcessAsync(receivedMessage, CancellationToken.None);

        Assert.True(firstCommit);
        Assert.True(secondCommit);
        Assert.Empty(harness.DeadLetters.Messages);
        Assert.Equal(1, harness.ProcessedEvents.InsertedCount);
        Assert.Equal(1, harness.UnitOfWork.SaveChangesCalls);

        var balance = Assert.Single(harness.DailyBalances.Items);
        Assert.Equal("merchant-001", balance.MerchantId);
        Assert.Equal(new DateOnly(2026, 6, 6), balance.Date);
        Assert.Equal("BRL", balance.Currency);
        Assert.Equal(150.00m, balance.TotalCredits);
        Assert.Equal(0m, balance.TotalDebits);
        Assert.Equal(150.00m, balance.NetBalance);
        Assert.Equal(ParseDateTimeOffset("2026-06-06T12:34:56.0000000Z"), balance.AsOf);
    }

    [Fact]
    public async Task Kafka_contract_fixture_should_map_headers_deserialize_and_project_balance()
    {
        using var harness = new ContractHarness();
        var payload = ReadContractExample("ledger-entry-created.v2.valid.json");
        var consumeResult = CreateKafkaResult(payload);

        var receivedMessage = KafkaReceivedMessageMapper.Map(consumeResult);

        Assert.Equal(payload, receivedMessage.Payload);
        Assert.Equal(LedgerEntryCreatedV2Contract.EventType, receivedMessage.EventType);
        Assert.Equal("evt-contract-kafka", receivedMessage.EventId);
        Assert.Equal("2cbdd495-586f-4565-a807-c5dc6710d237", receivedMessage.CorrelationId);
        Assert.Equal("merchant-001", receivedMessage.OrderingKey);
        Assert.Equal("kafka", receivedMessage.Transport.Provider);
        Assert.Equal(KafkaTopic, receivedMessage.Transport.Source);
        Assert.Equal("4", receivedMessage.Transport.Partition);
        Assert.Equal("123", receivedMessage.Transport.Offset);
        Assert.Equal("merchant-001", receivedMessage.Transport.Metadata["key"]);

        var shouldCommit = await harness.Processor.ProcessAsync(receivedMessage, CancellationToken.None);

        Assert.True(shouldCommit);
        Assert.Empty(harness.DeadLetters.Messages);

        var balance = Assert.Single(harness.DailyBalances.Items);
        Assert.Equal("merchant-001", balance.MerchantId);
        Assert.Equal(new DateOnly(2026, 6, 6), balance.Date);
        Assert.Equal(150.00m, balance.TotalCredits);
        Assert.Equal(150.00m, balance.NetBalance);
    }

    [Fact]
    public async Task Kafka_contract_fixture_should_ignore_duplicate_payload_without_projection_update()
    {
        using var harness = new ContractHarness();
        var payload = ReadContractExample("ledger-entry-created.v2.valid.json");
        var receivedMessage = KafkaReceivedMessageMapper.Map(CreateKafkaResult(payload));

        var firstCommit = await harness.Processor.ProcessAsync(receivedMessage, CancellationToken.None);
        var secondCommit = await harness.Processor.ProcessAsync(receivedMessage, CancellationToken.None);

        Assert.True(firstCommit);
        Assert.True(secondCommit);
        Assert.Empty(harness.DeadLetters.Messages);
        Assert.Equal(1, harness.ProcessedEvents.InsertedCount);
        Assert.Equal(1, harness.UnitOfWork.SaveChangesCalls);

        var balance = Assert.Single(harness.DailyBalances.Items);
        Assert.Equal("merchant-001", balance.MerchantId);
        Assert.Equal(new DateOnly(2026, 6, 6), balance.Date);
        Assert.Equal("BRL", balance.Currency);
        Assert.Equal(150.00m, balance.TotalCredits);
        Assert.Equal(0m, balance.TotalDebits);
        Assert.Equal(150.00m, balance.NetBalance);
    }

    [Fact]
    public async Task Legacy_v1_contract_fixture_should_project_with_documented_currency_fallback()
    {
        using var harness = new ContractHarness();
        var payload = ReadContractExample("ledger-entry-created.v1.valid.json");
        var receivedMessage = PubSubReceivedMessageMapper.Map(
            CreatePubSubMessage(payload, LedgerEntryCreatedV1Contract.EventType),
            PubSubSubscriptionId);

        var shouldAck = await harness.Processor.ProcessAsync(receivedMessage, CancellationToken.None);

        Assert.True(shouldAck);
        Assert.Empty(harness.DeadLetters.Messages);

        var balance = Assert.Single(harness.DailyBalances.Items);
        Assert.Equal("BRL", balance.Currency);
        Assert.Equal(150.00m, balance.TotalCredits);
    }

    [Fact]
    public async Task Invalid_v2_contract_fixture_should_be_rejected_safely_without_projection_update()
    {
        using var harness = new ContractHarness();
        var payload = ReadContractExample("ledger-entry-created.v2.invalid.json");
        var receivedMessage = PubSubReceivedMessageMapper.Map(CreatePubSubMessage(payload), PubSubSubscriptionId);

        var shouldAck = await harness.Processor.ProcessAsync(receivedMessage, CancellationToken.None);

        Assert.True(shouldAck);
        Assert.Empty(harness.DailyBalances.Items);
        Assert.Equal(0, harness.ProcessedEvents.InsertedCount);

        var deadLetter = Assert.Single(harness.DeadLetters.Messages);
        Assert.Equal(payload, deadLetter.OriginalPayload);
        Assert.Equal("Message payload currency is required.", deadLetter.Reason);
        Assert.Equal("MessageValidationException", deadLetter.ExceptionType);
        Assert.Equal(LedgerEntryCreatedV2Contract.EventType, deadLetter.EventType);
        Assert.Equal("pubsub", deadLetter.Provider);
        Assert.Equal(PubSubSubscriptionId, deadLetter.Source);
    }

    private static PubsubMessage CreatePubSubMessage(
        string payload,
        string eventType = LedgerEntryCreatedV2Contract.EventType)
    {
        var message = new PubsubMessage
        {
            Data = ByteString.CopyFromUtf8(payload),
            MessageId = "message-contract-1",
            OrderingKey = "merchant-001",
            PublishTime = ProtobufTimestamp.FromDateTimeOffset(ParseDateTimeOffset("2026-06-06T12:35:00.0000000Z"))
        };

        message.Attributes.Add(PubSubAttributeNames.EventType, eventType);
        message.Attributes.Add(PubSubAttributeNames.EventId, "evt-contract-pubsub");
        message.Attributes.Add(PubSubAttributeNames.CorrelationId, "2cbdd495-586f-4565-a807-c5dc6710d237");
        message.Attributes.Add(PubSubAttributeNames.TraceParent, "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
        message.Attributes.Add(PubSubAttributeNames.TraceState, "vendor=value");
        message.Attributes.Add(PubSubAttributeNames.Baggage, "tenant=poc");
        message.Attributes.Add(PubSubAttributeNames.DeliveryAttempt, "2");

        return message;
    }

    private static ConsumeResult<string, string> CreateKafkaResult(
        string payload,
        string eventType = LedgerEntryCreatedV2Contract.EventType)
        => new()
        {
            Topic = KafkaTopic,
            Partition = new Partition(4),
            Offset = new Offset(123),
            Message = new Message<string, string>
            {
                Key = "merchant-001",
                Value = payload,
                Headers = new Headers
                {
                    { MessageAttributeNames.EventType, TextEncoding.UTF8.GetBytes(eventType) },
                    { MessageAttributeNames.EventId, TextEncoding.UTF8.GetBytes("evt-contract-kafka") },
                    { MessageAttributeNames.CorrelationId, TextEncoding.UTF8.GetBytes("2cbdd495-586f-4565-a807-c5dc6710d237") },
                    { MessageAttributeNames.TraceParent, TextEncoding.UTF8.GetBytes("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01") },
                    { MessageAttributeNames.TraceState, TextEncoding.UTF8.GetBytes("vendor=value") },
                    { MessageAttributeNames.Baggage, TextEncoding.UTF8.GetBytes("tenant=poc") }
                }
            }
        };

    private static DateTimeOffset ParseDateTimeOffset(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static string ReadContractExample(string fileName)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), "contracts", "events", "examples", fileName));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class ContractHarness : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;

        public ContractHarness()
        {
            DailyBalances = new InMemoryDailyBalanceRepository();
            ProcessedEvents = new InMemoryProcessedEventRepository();
            UnitOfWork = new InMemoryUnitOfWork();
            DeadLetters = new CapturingDeadLetterPublisher();

            var services = new ServiceCollection();
            services.AddApplication();
            services.AddLogging();
            services.AddSingleton<IDailyBalanceRepository>(DailyBalances);
            services.AddSingleton<IProcessedEventRepository>(ProcessedEvents);
            services.AddSingleton<IUnitOfWork>(UnitOfWork);
            services.AddSingleton<IClock>(new FixedClock(ParseDateTimeOffset("2026-06-06T12:35:30.0000000Z")));

            _serviceProvider = services.BuildServiceProvider();
            Metrics = new MessagingMetrics($"{MessagingMetrics.MeterName}.ContractTests.{Guid.NewGuid():N}");
            Processor = new LedgerEntryCreatedMessageProcessor(
                _serviceProvider,
                DeadLetters,
                Metrics,
                NullLogger<LedgerEntryCreatedMessageProcessor>.Instance);
        }

        public InMemoryDailyBalanceRepository DailyBalances
        {
            get;
        }
        public InMemoryProcessedEventRepository ProcessedEvents
        {
            get;
        }
        public InMemoryUnitOfWork UnitOfWork
        {
            get;
        }
        public CapturingDeadLetterPublisher DeadLetters
        {
            get;
        }
        public MessagingMetrics Metrics
        {
            get;
        }
        public LedgerEntryCreatedMessageProcessor Processor
        {
            get;
        }

        public void Dispose()
        {
            Metrics.Dispose();
            _serviceProvider.Dispose();
        }
    }

    private sealed class InMemoryDailyBalanceRepository : IDailyBalanceRepository
    {
        private readonly Dictionary<(string MerchantId, DateOnly Date, string Currency), DailyBalance> _balances = [];

        public IReadOnlyCollection<DailyBalance> Items => _balances.Values;

        public Task LockByMerchantDateAndCurrencyAsync(
            string merchantId,
            DateOnly date,
            string currency,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<DailyBalance?> GetByMerchantDateAndCurrencyAsync(
            string merchantId,
            DateOnly date,
            string currency,
            CancellationToken cancellationToken = default)
        {
            _balances.TryGetValue((merchantId, date, currency), out var balance);
            return Task.FromResult(balance);
        }

        public Task AddAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default)
        {
            _balances[(dailyBalance.MerchantId, dailyBalance.Date, dailyBalance.Currency)] = dailyBalance;
            return Task.CompletedTask;
        }

        public Task<int> DeleteByMerchantAndDateRangeAsync(
            string merchantId,
            DateOnly from,
            DateOnly until,
            CancellationToken cancellationToken = default)
        {
            var keys = _balances.Keys
                .Where(x => x.MerchantId == merchantId && x.Date >= from && x.Date <= until)
                .ToArray();

            foreach (var key in keys)
                _balances.Remove(key);

            return Task.FromResult(keys.Length);
        }
    }

    private sealed class InMemoryProcessedEventRepository : IProcessedEventRepository
    {
        private readonly HashSet<string> _eventIds = new(StringComparer.Ordinal);

        public int InsertedCount
        {
            get; private set;
        }

        public Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(_eventIds.Contains(eventId));

        public Task<bool> TryInsertAsync(ProcessedEvent processedEvent, CancellationToken cancellationToken = default)
        {
            if (!_eventIds.Add(processedEvent.EventId))
                return Task.FromResult(false);

            InsertedCount++;
            return Task.FromResult(true);
        }

        public Task<int> DeleteByEventIdsAsync(
            IReadOnlyCollection<string> eventIds,
            CancellationToken cancellationToken = default)
        {
            var deleted = eventIds.Count(_eventIds.Remove);
            return Task.FromResult(deleted);
        }
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls
        {
            get; private set;
        }

        public Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IAppTransaction>(new NoopTransaction());

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class NoopTransaction : IAppTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class CapturingDeadLetterPublisher : IDeadLetterPublisher
    {
        public List<DeadLetterMessage> Messages { get; } = [];

        public Task PublishAsync(DeadLetterMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
