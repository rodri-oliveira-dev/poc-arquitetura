using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TransferService.Api.Contracts.Responses;
using TransferService.Application.Abstractions.Messaging;
using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Abstractions.Time;
using TransferService.Application.Transferencias.Events;
using TransferService.Domain.Sagas;
using TransferService.Infrastructure.Messaging.Kafka;
using TransferService.Infrastructure.Persistence;
using TransferService.Infrastructure.Persistence.Outbox;
using TransferService.Infrastructure.Persistence.Repositories;
using TransferService.IntegrationTests.Infrastructure;
using TransferService.IntegrationTests.Infrastructure.Security;
using TransferService.Worker.Ledger;
using TransferService.Worker.Messaging;
using TransferService.Worker.Options;
using TransferService.Worker.Outbox;
using TransferService.Worker.Sagas;

namespace TransferService.IntegrationTests.Sagas;

[Trait("Category", "Integration")]
public sealed class TransferenciaSagaKafkaFlowTests : IClassFixture<TransferApiFactory>
{
    private readonly TransferApiFactory _factory;
    private readonly HttpClient _client;

    public TransferenciaSagaKafkaFlowTests(TransferApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Full_flow_should_create_pending_saga_process_ledger_and_publish_kafka_events()
    {
        var ledger = new FakeLedgerClient();
        var kafka = new FakeKafkaProducer();
        await using var harness = CreateWorkerHarness(ledger, kafka);
        var correlationId = Guid.NewGuid().ToString();

        var created = await PostTransferenciaAsync(correlationId);

        await using (var db = _factory.CreateDbContext())
        {
            var pending = await db.TransferenciasSagas.SingleAsync(x => x.Id == created.TransferenciaId, TestContext.Current.CancellationToken);
            Assert.Equal(TransferenciaSagaStatus.Pending, pending.Status);
        }

        await harness.SagaProcessor.ProcessOnceAsync(TestContext.Current.CancellationToken);
        await harness.OutboxPublisher.PublishOnceAsync(TestContext.Current.CancellationToken);

        await using (var db = _factory.CreateDbContext())
        {
            var completed = await db.TransferenciasSagas.SingleAsync(x => x.Id == created.TransferenciaId, TestContext.Current.CancellationToken);
            Assert.Equal(TransferenciaSagaStatus.Completed, completed.Status);
            Assert.NotNull(completed.DebitLancamentoId);
            Assert.NotNull(completed.CreditLancamentoId);

            var eventTypes = await db.OutboxMessages
                .Where(x => x.AggregateId == created.TransferenciaId)
                .Select(x => x.EventType)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Contains(TransferenciaSolicitadaV1.Type, eventTypes);
            Assert.Contains(TransferenciaDebitoCriadoV1.Type, eventTypes);
            Assert.Contains(TransferenciaCreditoCriadoV1.Type, eventTypes);
            Assert.Contains(TransferenciaConcluidaV1.Type, eventTypes);
            Assert.All(db.OutboxMessages.Where(x => x.AggregateId == created.TransferenciaId), x => Assert.Equal(TransferenciaOutboxStatus.Published, x.Status));
        }

        Assert.Equal(2, ledger.CreateLancamentoCalls.Count);
        Assert.Contains(ledger.CreateLancamentoCalls, x => x.Request.Type == "DEBIT" && x.Request.Amount < 0);
        Assert.Contains(ledger.CreateLancamentoCalls, x => x.Request.Type == "CREDIT" && x.Request.Amount > 0);
        Assert.All(ledger.CreateLancamentoCalls, x => Assert.Equal(correlationId, x.CorrelationId));
        Assert.Contains(kafka.Published, x => x.Topic == "transfer.transferencia.solicitada" && x.Message.MessageKey == created.TransferenciaId.ToString());
        Assert.Contains(kafka.Published, x => x.Topic == "transfer.transferencia.debito-criado" && x.Message.MessageKey == created.TransferenciaId.ToString());
        Assert.Contains(kafka.Published, x => x.Topic == "transfer.transferencia.credito-criado" && x.Message.MessageKey == created.TransferenciaId.ToString());
        Assert.Contains(kafka.Published, x => x.Topic == "transfer.transferencia.concluida" && x.Message.MessageKey == created.TransferenciaId.ToString());
        Assert.Contains(harness.Logs, x => x.Contains(correlationId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Compensable_failure_should_request_debit_compensation_and_publish_compensation_event()
    {
        var ledger = new FakeLedgerClient { FailCredit = true };
        var kafka = new FakeKafkaProducer();
        await using var harness = CreateWorkerHarness(ledger, kafka);

        var created = await PostTransferenciaAsync(Guid.NewGuid().ToString());

        await harness.SagaProcessor.ProcessOnceAsync(TestContext.Current.CancellationToken);
        await harness.OutboxPublisher.PublishOnceAsync(TestContext.Current.CancellationToken);

        await using var db = _factory.CreateDbContext();
        var saga = await db.TransferenciasSagas.SingleAsync(x => x.Id == created.TransferenciaId, TestContext.Current.CancellationToken);
        Assert.Equal(TransferenciaSagaStatus.CompensationRequested, saga.Status);
        Assert.NotNull(saga.DebitLancamentoId);
        Assert.Null(saga.CreditLancamentoId);
        Assert.NotNull(saga.CompensationEstornoId);

        Assert.Single(ledger.EstornoCalls);
        Assert.Contains(db.OutboxMessages, x =>
            x.AggregateId == created.TransferenciaId &&
            x.EventType == TransferenciaCompensacaoSolicitadaV1.Type &&
            x.Status == TransferenciaOutboxStatus.Published);
        Assert.Contains(kafka.Published, x =>
            x.Topic == "transfer.transferencia.compensacao-solicitada" &&
            x.Message.MessageKey == created.TransferenciaId.ToString());
    }

    [Fact]
    public async Task Reprocessing_completed_saga_should_not_duplicate_ledger_entries()
    {
        var ledger = new FakeLedgerClient();
        var kafka = new FakeKafkaProducer();
        await using var harness = CreateWorkerHarness(ledger, kafka);
        var created = await PostTransferenciaAsync(Guid.NewGuid().ToString());

        await harness.SagaProcessor.ProcessOnceAsync(TestContext.Current.CancellationToken);
        await harness.SagaProcessor.ProcessOnceAsync(TestContext.Current.CancellationToken);
        await harness.OutboxPublisher.PublishOnceAsync(TestContext.Current.CancellationToken);
        await harness.OutboxPublisher.PublishOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, ledger.CreateLancamentoCalls.Count);
        Assert.Single(ledger.CreateLancamentoCalls, x => x.Request.Type == "DEBIT");
        Assert.Single(ledger.CreateLancamentoCalls, x => x.Request.Type == "CREDIT");
        Assert.Equal(kafka.Published.Select(x => x.Message.Id).Distinct().Count(), kafka.Published.Count);

        await using var db = _factory.CreateDbContext();
        var saga = await db.TransferenciasSagas.SingleAsync(x => x.Id == created.TransferenciaId, TestContext.Current.CancellationToken);
        Assert.Equal(TransferenciaSagaStatus.Completed, saga.Status);
    }

    private WorkerHarness CreateWorkerHarness(FakeLedgerClient ledger, FakeKafkaProducer kafka)
    {
        var services = new ServiceCollection();
#pragma warning disable CA2000 // O WorkerHarness descarta o provider junto com o ServiceProvider.
        var loggerProvider = new InMemoryLoggerProvider();
#pragma warning restore CA2000
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddDbContext<TransferServiceDbContext>(options => _factory.ConfigureDbContext(options));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TransferServiceDbContext>());
        services.AddScoped<ITransferenciaSagaRepository, TransferenciaSagaRepository>();
        services.AddScoped<ITransferenciaOutboxWriter, TransferenciaOutboxWriter>();
        services.AddSingleton(new TransferenciaSagaKafkaMetadataMapper(Options.Create(new TransferenciaKafkaTopicOptions())));
        services.AddSingleton<IClock>(new FixedClock());
        services.AddSingleton<ILedgerServiceClient>(ledger);
        services.AddSingleton<ITransferenciaKafkaProducer>(kafka);
        services.AddSingleton(Options.Create(new TransferWorkerOptions
        {
            Enabled = true,
            BatchSize = 10,
            MaxRetryCount = 3,
            PollingInterval = TimeSpan.FromMilliseconds(1),
            RetryBackoff = TimeSpan.FromMilliseconds(1),
            LockDuration = TimeSpan.FromMinutes(1),
            Kafka = { BootstrapServers = "localhost:9092" }
        }));
        services.AddSingleton<TransferenciaSagaProcessorService>();
        services.AddSingleton<TransferenciaOutboxPublisherService>();

        var provider = services.BuildServiceProvider(validateScopes: true);
        return new WorkerHarness(
            provider,
            provider.GetRequiredService<TransferenciaSagaProcessorService>(),
            provider.GetRequiredService<TransferenciaOutboxPublisherService>(),
            loggerProvider.Messages,
            loggerProvider);
    }

    private async Task<SolicitarTransferenciaResponse> PostTransferenciaAsync(string correlationId)
    {
        Authenticate();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transferencias")
        {
            Content = JsonContent.Create(new
            {
                sourceMerchantId = "m1",
                destinationMerchantId = "m2",
                amount = 100m,
                description = "Transferencia integrada",
                externalReference = $"it-{Guid.NewGuid():N}"
            })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        req.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(req, TestContext.Current.CancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Accepted,
            $"Expected HTTP {(int)HttpStatusCode.Accepted} but got {(int)response.StatusCode}: {responseText}{Environment.NewLine}{string.Join(Environment.NewLine, _factory.LogMessages)}");
        var body = await response.Content.ReadFromJsonAsync<SolicitarTransferenciaResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        return body;
    }

    private void Authenticate()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: TestJwtTokenFactory.TransferAudience,
            scopes: "transfer.write transfer.read",
            merchantIds: "m1 m2");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed record WorkerHarness(
        ServiceProvider ServiceProvider,
        TransferenciaSagaProcessorService SagaProcessor,
        TransferenciaOutboxPublisherService OutboxPublisher,
        IReadOnlyList<string> Logs,
        InMemoryLoggerProvider LoggerProvider) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await ServiceProvider.DisposeAsync();
            LoggerProvider.Dispose();
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class FakeLedgerClient : ILedgerServiceClient
    {
        private readonly Dictionary<string, Guid> _lancamentosByKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Guid> _estornosByKey = new(StringComparer.Ordinal);

        public bool FailCredit
        {
            get; init;
        }
        public List<(CreateLedgerLancamentoRequest Request, string IdempotencyKey, string? CorrelationId)> CreateLancamentoCalls { get; } = [];
        public List<(Guid LancamentoId, string IdempotencyKey, string? CorrelationId)> EstornoCalls { get; } = [];

        public Task<LedgerLancamentoResult> CreateLancamentoAsync(
            CreateLedgerLancamentoRequest request,
            string idempotencyKey,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FailCredit && request.Type == "CREDIT")
                throw new LedgerServiceException(HttpStatusCode.BadRequest, "credito recusado");

            if (!_lancamentosByKey.TryGetValue(idempotencyKey, out var lancamentoId))
            {
                lancamentoId = Guid.NewGuid();
                _lancamentosByKey[idempotencyKey] = lancamentoId;
                CreateLancamentoCalls.Add((request, idempotencyKey, correlationId));
            }

            return Task.FromResult(new LedgerLancamentoResult(lancamentoId));
        }

        public Task<LedgerEstornoResult> SolicitarEstornoAsync(
            Guid lancamentoId,
            SolicitarLedgerEstornoRequest request,
            string idempotencyKey,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_estornosByKey.TryGetValue(idempotencyKey, out var estornoId))
            {
                estornoId = Guid.NewGuid();
                _estornosByKey[idempotencyKey] = estornoId;
                EstornoCalls.Add((lancamentoId, idempotencyKey, correlationId));
            }

            return Task.FromResult(new LedgerEstornoResult(estornoId));
        }
    }

    private sealed class FakeKafkaProducer : ITransferenciaKafkaProducer
    {
        public List<(TransferenciaOutboxMessage Message, string Topic)> Published { get; } = [];
        public List<(TransferenciaOutboxMessage Message, string Topic, string Reason)> Dlq { get; } = [];

        public Task PublishAsync(TransferenciaOutboxMessage message, string topic, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Published.Add((message, topic));
            return Task.CompletedTask;
        }

        public Task PublishDlqAsync(TransferenciaOutboxMessage message, string reason, string dlqTopic, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dlq.Add((message, dlqTopic, reason));
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, _messages);

        public void Dispose()
        {
        }

        private sealed class InMemoryLogger(string categoryName, List<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull
                => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var message = $"{logLevel}: {categoryName}: {formatter(state, exception)}";
                if (exception is not null)
                    message += Environment.NewLine + exception;

                lock (messages)
                {
                    messages.Add(message);
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
