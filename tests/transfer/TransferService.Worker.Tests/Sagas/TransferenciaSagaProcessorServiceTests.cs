using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using TransferService.Application.Abstractions.Messaging;
using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Abstractions.Time;
using TransferService.Application.Transferencias.Events;
using TransferService.Domain.Sagas;
using TransferService.Infrastructure.Messaging.Kafka;
using TransferService.Infrastructure.Persistence;
using TransferService.Infrastructure.Persistence.Outbox;
using TransferService.Infrastructure.Persistence.Repositories;
using TransferService.Worker.Ledger;
using TransferService.Worker.Messaging;
using TransferService.Worker.Options;
using TransferService.Worker.Outbox;
using TransferService.Worker.Sagas;

using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace TransferService.Worker.Tests.Sagas;

public sealed class TransferenciaSagaProcessorServiceTests
{
    [Fact]
    public async Task ProcessOnceAsync_should_complete_saga_and_write_outbox_events_Async()
    {
        var fixture = new WorkerFixture();
        var saga = fixture.AddSaga();
        fixture.Ledger.EnqueueLancamento(Guid.NewGuid());
        fixture.Ledger.EnqueueLancamento(Guid.NewGuid());

        await fixture.SagaProcessor.ProcessOnceAsync(TestContext.Current.CancellationToken);

        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.TransferenciasSagas.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TransferenciaSagaStatus.Completed, stored.Status);
        Assert.True(stored.DebitCreated);
        Assert.True(stored.CreditCreated);
        Assert.Contains(fixture.Db.OutboxMessages, x => x.EventType == TransferenciaDebitoCriadoV1.Type);
        Assert.Contains(fixture.Db.OutboxMessages, x => x.EventType == TransferenciaCreditoCriadoV1.Type);
        Assert.Contains(fixture.Db.OutboxMessages, x => x.EventType == TransferenciaConcluidaV1.Type);
        Assert.Equal(2, fixture.Ledger.IdempotencyKeys.Count);
        Assert.All(fixture.Ledger.IdempotencyKeys, AssertValidUuidIdempotencyKey);
        Assert.NotEqual(fixture.Ledger.IdempotencyKeys[0], fixture.Ledger.IdempotencyKeys[1]);
    }

    [Fact]
    public async Task ProcessOnceAsync_should_request_compensation_when_credit_fails_after_debit_Async()
    {
        var fixture = new WorkerFixture();
        var saga = fixture.AddSaga();
        var debitId = Guid.NewGuid();
        fixture.Ledger.EnqueueLancamento(debitId);
        fixture.Ledger.FailNextLancamento(new LedgerServiceException(System.Net.HttpStatusCode.BadRequest, "credito invalido"));
        fixture.Ledger.EnqueueEstorno(Guid.NewGuid());

        await fixture.SagaProcessor.ProcessOnceAsync(TestContext.Current.CancellationToken);

        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.TransferenciasSagas.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TransferenciaSagaStatus.CompensationRequested, stored.Status);
        Assert.Equal(debitId, stored.DebitLancamentoId);
        Assert.NotNull(stored.CompensationEstornoId);
        Assert.Contains(fixture.Db.OutboxMessages, x => x.EventType == TransferenciaCompensacaoSolicitadaV1.Type);
        Assert.Equal(3, fixture.Ledger.IdempotencyKeys.Count);
        Assert.All(fixture.Ledger.IdempotencyKeys, AssertValidUuidIdempotencyKey);
        Assert.Equal(3, fixture.Ledger.IdempotencyKeys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task ProcessOnceAsync_should_schedule_retry_before_debit_on_transient_failure_Async()
    {
        var fixture = new WorkerFixture();
        fixture.AddSaga();
        fixture.Ledger.FailNextLancamento(new LedgerServiceException(System.Net.HttpStatusCode.ServiceUnavailable, "temporario"));

        await fixture.SagaProcessor.ProcessOnceAsync(TestContext.Current.CancellationToken);

        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.TransferenciasSagas.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(TransferenciaSagaStatus.Failed, stored.Status);
        Assert.Equal(1, stored.RetryCount);
        Assert.NotNull(stored.NextRetryAt);
    }

    [Fact]
    public async Task ProcessOnceAsync_should_continue_with_next_saga_after_unexpected_failure_Async()
    {
        var fixture = new WorkerFixture();
        var failedSaga = fixture.AddSaga();
        var completedSaga = fixture.AddSaga();
        fixture.Ledger.FailNextLancamento(new InvalidOperationException("Unexpected persistence or dependency failure."));
        fixture.Ledger.EnqueueLancamento(Guid.NewGuid());
        fixture.Ledger.EnqueueLancamento(Guid.NewGuid());

        await fixture.SagaProcessor.ProcessOnceAsync(TestContext.Current.CancellationToken);

        fixture.Db.ChangeTracker.Clear();
        var storedFailedSaga = await fixture.Db.TransferenciasSagas.SingleAsync(
            x => x.Id == failedSaga.Id,
            TestContext.Current.CancellationToken);
        var storedCompletedSaga = await fixture.Db.TransferenciasSagas.SingleAsync(
            x => x.Id == completedSaga.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(TransferenciaSagaStatus.DebitCreating, storedFailedSaga.Status);
        Assert.Equal(TransferenciaSagaStatus.Completed, storedCompletedSaga.Status);
    }

    [Fact]
    public async Task ProcessOnceAsync_should_respect_cancelled_token_Async()
    {
        var fixture = new WorkerFixture();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.SagaProcessor.ProcessOnceAsync(cts.Token));
    }

    [Fact]
    public async Task ProcessOnceAsync_should_not_duplicate_debit_when_retrying_after_debit_created_Async()
    {
        var fixture = new WorkerFixture();
        var saga = fixture.AddSaga();
        saga.MarkClaimedForProcessing("test", fixture.Clock.UtcNow.AddMinutes(-1), fixture.Clock.UtcNow);
        saga.MarkDebitCreating(fixture.Clock.UtcNow);
        saga.MarkDebitCreated(fixture.Clock.UtcNow, Guid.NewGuid());
        saga.ScheduleRetry(fixture.Clock.UtcNow.AddSeconds(-1), "retry credito", fixture.Clock.UtcNow);
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Ledger.EnqueueLancamento(Guid.NewGuid());

        await fixture.SagaProcessor.ProcessOnceAsync(TestContext.Current.CancellationToken);

        fixture.Db.ChangeTracker.Clear();
        Assert.Single(fixture.Ledger.CreateLancamentoRequests);
        Assert.Equal("CREDIT", fixture.Ledger.CreateLancamentoRequests[0].Type);
    }

    [Fact]
    public async Task ProcessOnceAsync_should_ignore_completed_saga_Async()
    {
        var fixture = new WorkerFixture();
        var saga = fixture.AddSaga();
        saga.MarkClaimedForProcessing("test", fixture.Clock.UtcNow.AddMinutes(-1), fixture.Clock.UtcNow);
        saga.MarkDebitCreating(fixture.Clock.UtcNow);
        saga.MarkDebitCreated(fixture.Clock.UtcNow, Guid.NewGuid());
        saga.MarkCreditCreating(fixture.Clock.UtcNow);
        saga.MarkCompleted(fixture.Clock.UtcNow, Guid.NewGuid());
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await fixture.SagaProcessor.ProcessOnceAsync(TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Ledger.CreateLancamentoRequests);
    }

    [Fact]
    public async Task PublishOnceAsync_should_publish_pending_outbox_with_transferencia_key_and_topic_Async()
    {
        var fixture = new WorkerFixture();
        var saga = fixture.AddSaga();
        await fixture.OutboxWriter.WriteAsync(
            TransferenciaSagaEventFactory.TransferenciaDebitoCriado(saga, saga.CorrelationId, fixture.Clock.UtcNow),
            TestContext.Current.CancellationToken);
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await fixture.OutboxPublisher.PublishOnceAsync(TestContext.Current.CancellationToken);

        fixture.Db.ChangeTracker.Clear();
        var (Message, Topic) = Assert.Single(fixture.Kafka.Published);
        Assert.Equal(saga.Id.ToString(), Message.MessageKey);
        Assert.Equal("transfer.transferencia.debito-criado", Topic);
        Assert.Equal(TransferenciaOutboxStatus.Published, fixture.Db.OutboxMessages.Single().Status);
    }

    [Fact]
    public async Task PublishOnceAsync_should_not_republish_published_message_Async()
    {
        var fixture = new WorkerFixture();
        var message = new TransferenciaOutboxMessage(
            "TransferenciaSaga",
            Guid.NewGuid(),
            TransferenciaConcluidaV1.Type,
            "{}",
            "transfer.transferencia.concluida",
            Guid.NewGuid().ToString(),
            null,
            fixture.Clock.UtcNow,
            fixture.Clock.UtcNow);
        message.MarkPublished(fixture.Clock.UtcNow);
        await fixture.Db.OutboxMessages.AddAsync(message, TestContext.Current.CancellationToken);
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await fixture.OutboxPublisher.PublishOnceAsync(TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Kafka.Published);
    }

    [Fact]
    public async Task PublishOnceAsync_should_keep_message_pending_on_transient_kafka_error_Async()
    {
        var fixture = new WorkerFixture();
        await fixture.AddOutboxMessageAsync();
        fixture.Kafka.PublishException = new TransferenciaKafkaPublishException("temporario", isTransient: true);

        await fixture.OutboxPublisher.PublishOnceAsync(TestContext.Current.CancellationToken);

        fixture.Db.ChangeTracker.Clear();
        var message = fixture.Db.OutboxMessages.Single();
        Assert.Equal(TransferenciaOutboxStatus.Pending, message.Status);
        Assert.NotNull(message.NextRetryAt);
    }

    [Fact]
    public async Task PublishOnceAsync_should_send_to_dlq_on_definitive_kafka_error_Async()
    {
        var fixture = new WorkerFixture();
        await fixture.AddOutboxMessageAsync();
        fixture.Kafka.PublishException = new TransferenciaKafkaPublishException("definitivo", isTransient: false);

        await fixture.OutboxPublisher.PublishOnceAsync(TestContext.Current.CancellationToken);

        fixture.Db.ChangeTracker.Clear();
        Assert.Single(fixture.Kafka.Dlq);
        Assert.Equal("transfer.transferencia.dlq", fixture.Kafka.Dlq[0].Topic);
        Assert.Equal(TransferenciaOutboxStatus.DeadLetter, fixture.Db.OutboxMessages.Single().Status);
    }

    [Fact]
    public async Task PublishOnceAsync_should_send_invalid_payload_to_dlq_Async()
    {
        var fixture = new WorkerFixture();
        var message = new TransferenciaOutboxMessage(
            "TransferenciaSaga",
            Guid.NewGuid(),
            TransferenciaConcluidaV1.Type,
            "{",
            "transfer.transferencia.concluida",
            Guid.NewGuid().ToString(),
            null,
            fixture.Clock.UtcNow,
            fixture.Clock.UtcNow);
        await fixture.Db.OutboxMessages.AddAsync(message, TestContext.Current.CancellationToken);
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await fixture.OutboxPublisher.PublishOnceAsync(TestContext.Current.CancellationToken);

        fixture.Db.ChangeTracker.Clear();
        Assert.Single(fixture.Kafka.Dlq);
        Assert.Equal(TransferenciaOutboxStatus.DeadLetter, fixture.Db.OutboxMessages.Single().Status);
    }


    [Fact]
    public async Task PublishOnceAsync_should_respect_cancelled_token_Async()
    {
        var fixture = new WorkerFixture();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.OutboxPublisher.PublishOnceAsync(cts.Token));
    }

    private sealed class WorkerFixture
    {
        private readonly ServiceProvider _provider;

        public WorkerFixture()
        {
            var services = new ServiceCollection();
            var databaseName = Guid.NewGuid().ToString();
            services.AddDbContext<TransferServiceDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TransferServiceDbContext>());
            services.AddScoped<ITransferenciaSagaRepository, TransferenciaSagaRepository>();
            services.AddScoped<ITransferenciaOutboxWriter, TransferenciaOutboxWriter>();
            services.AddSingleton(new TransferenciaSagaKafkaMetadataMapper(OptionsFactory.Create(new TransferenciaKafkaTopicOptions())));
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton(Ledger);
            services.AddSingleton<ILedgerServiceClient>(Ledger);
            services.AddSingleton(Kafka);
            services.AddSingleton<ITransferenciaKafkaProducer>(Kafka);
            services.AddSingleton(OptionsFactory.Create(new TransferWorkerOptions
            {
                PollingInterval = TimeSpan.FromMilliseconds(1),
                BatchSize = 10,
                MaxRetryCount = 3,
                RetryBackoff = TimeSpan.FromSeconds(10),
                Kafka = { BootstrapServers = "localhost:9092" }
            }));
            services.AddSingleton<TransferenciaSagaProcessorService>(sp => new(
                sp,
                sp.GetRequiredService<IOptions<TransferWorkerOptions>>(),
                sp.GetRequiredService<IClock>(),
                NullLogger<TransferenciaSagaProcessorService>.Instance));
            services.AddSingleton<TransferenciaOutboxPublisherService>(sp => new(
                sp,
                sp.GetRequiredService<IOptions<TransferWorkerOptions>>(),
                sp.GetRequiredService<IClock>(),
                NullLogger<TransferenciaOutboxPublisherService>.Instance));

            _provider = services.BuildServiceProvider();
            Db = _provider.GetRequiredService<TransferServiceDbContext>();
            SagaProcessor = _provider.GetRequiredService<TransferenciaSagaProcessorService>();
            OutboxPublisher = _provider.GetRequiredService<TransferenciaOutboxPublisherService>();
            OutboxWriter = _provider.GetRequiredService<ITransferenciaOutboxWriter>();
        }

        public TransferServiceDbContext Db
        {
            get;
        }
        public FakeClock Clock { get; } = new();
        public FakeLedgerClient Ledger { get; } = new();
        public FakeKafkaProducer Kafka { get; } = new();
        public TransferenciaSagaProcessorService SagaProcessor
        {
            get;
        }
        public TransferenciaOutboxPublisherService OutboxPublisher
        {
            get;
        }
        public ITransferenciaOutboxWriter OutboxWriter
        {
            get;
        }

        public TransferenciaSaga AddSaga()
        {
            var saga = new TransferenciaSaga(
                new MerchantId("merchant-source"),
                new MerchantId("merchant-destination"),
                new TransferAmount(10m),
                Clock.UtcNow);
            saga.RegisterRequestMetadata("idem", "hash", Guid.NewGuid().ToString(), Clock.UtcNow);
            Db.TransferenciasSagas.Add(saga);
            Db.SaveChanges();
            return saga;
        }

        public async Task AddOutboxMessageAsync()
        {
            var saga = AddSaga();
            await OutboxWriter.WriteAsync(
                TransferenciaSagaEventFactory.TransferenciaConcluida(saga, saga.CorrelationId, Clock.UtcNow),
                TestContext.Current.CancellationToken);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private static void AssertValidUuidIdempotencyKey(string value)
        => Assert.True(Guid.TryParse(value, out _), $"Idempotency-Key '{value}' deve ser UUID valido.");

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeLedgerClient : ILedgerServiceClient
    {
        private readonly Queue<object> _lancamentos = new();
        private readonly Queue<Guid> _estornos = new();

        public List<CreateLedgerLancamentoRequest> CreateLancamentoRequests { get; } = [];
        public List<string> IdempotencyKeys { get; } = [];

        public void EnqueueLancamento(Guid lancamentoId) => _lancamentos.Enqueue(lancamentoId);
        public void FailNextLancamento(Exception exception) => _lancamentos.Enqueue(exception);
        public void EnqueueEstorno(Guid estornoId) => _estornos.Enqueue(estornoId);

        public Task<LedgerLancamentoResult> CreateLancamentoAsync(
            CreateLedgerLancamentoRequest request,
            string idempotencyKey,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateLancamentoRequests.Add(request);
            IdempotencyKeys.Add(idempotencyKey);
            var next = _lancamentos.Dequeue();
            return next is Exception exception ? throw exception : Task.FromResult(new LedgerLancamentoResult((Guid)next));
        }

        public Task<LedgerEstornoResult> SolicitarEstornoAsync(
            Guid lancamentoId,
            SolicitarLedgerEstornoRequest request,
            string idempotencyKey,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IdempotencyKeys.Add(idempotencyKey);
            return Task.FromResult(new LedgerEstornoResult(_estornos.Dequeue()));
        }
    }

    private sealed class FakeKafkaProducer : ITransferenciaKafkaProducer
    {
        public TransferenciaKafkaPublishException? PublishException
        {
            get; set;
        }
        public List<(TransferenciaOutboxMessage Message, string Topic)> Published { get; } = [];
        public List<(TransferenciaOutboxMessage Message, string Topic, string Reason)> Dlq { get; } = [];

        public Task PublishAsync(TransferenciaOutboxMessage message, string topic, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PublishException is not null)
            {
                throw PublishException;
            }

            Published.Add((message, topic));
            return Task.CompletedTask;
        }

        public Task PublishDlqAsync(
            TransferenciaOutboxMessage message,
            string reason,
            string dlqTopic,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dlq.Add((message, dlqTopic, reason));
            return Task.CompletedTask;
        }
    }
}
