using System.Text.Json;

using IdentityService.Application.Common.DomainEvents;
using IdentityService.Application.Idempotency;
using IdentityService.Application.Idempotency.Ports;
using IdentityService.Domain.Users;
using IdentityService.Infrastructure.Persistence;
using IdentityService.IntegrationTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace IdentityService.IntegrationTests.Persistence;

[Trait("Category", "Container")]
[Trait("Category", "Integration")]
[Collection(PostgresIdentityCollection.Name)]
public sealed class IdentityPersistenceTests(PostgresIdentityFixture fixture) : IAsyncLifetime
{
    private readonly PostgresIdentityFixture _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        await _fixture.CleanAsync();
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public async Task Repository_should_persist_user_with_runtime_identity_app_user()
    {
        await using var provider = _fixture.CreateServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<Application.Users.Ports.IUserRepository>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = CreateUser(email: "persisted@example.com", keycloakUserId: "kc-persisted");

        await repository.AddAsync(user, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var persisted = await db.Users
            .AsNoTracking()
            .SingleAsync(x => x.Id == user.Id, TestContext.Current.CancellationToken);

        Assert.Equal(user.Id, persisted.Id);
        Assert.Equal(user.Email, persisted.Email);
        Assert.Equal(user.Username, persisted.Username);
        Assert.Equal(user.MerchantId, persisted.MerchantId);
        Assert.Equal(user.KeycloakUserId, persisted.KeycloakUserId);
    }

    [Fact]
    public async Task Database_should_enforce_unique_email()
    {
        await using var db = _fixture.CreateDbContext();

        db.Users.Add(CreateUser(email: "unique-email@example.com", keycloakUserId: "kc-email-1"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Users.Add(CreateUser(email: "unique-email@example.com", keycloakUserId: "kc-email-2"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("ux_identity_users_email", postgresException.ConstraintName);
    }

    [Fact]
    public async Task Database_should_enforce_unique_keycloak_user_id()
    {
        await using var db = _fixture.CreateDbContext();

        db.Users.Add(CreateUser(email: "keycloak-1@example.com", keycloakUserId: "kc-unique"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Users.Add(CreateUser(email: "keycloak-2@example.com", keycloakUserId: "kc-unique"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("ux_identity_users_keycloak_user_id", postgresException.ConstraintName);
    }

    [Fact]
    public async Task Migration_should_create_users_table_in_identity_schema()
    {
        await using var db = _fixture.CreateDbContext();

        await db.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT table_schema
            FROM information_schema.tables
            WHERE table_schema = 'identity' AND table_name = 'users';
            """;

        var schema = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal("identity", schema);
        Assert.Equal("identity", db.Model.GetDefaultSchema());
        Assert.Equal(
            "identity",
            db.Model.FindEntityType(typeof(User))?.GetSchema());
    }

    [Fact]
    public async Task Idempotency_repository_should_persist_processing_record()
    {
        await using var provider = _fixture.CreateServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIdempotencyRepository>();
        var record = CreateProcessingRecord("CreateUser", "idem-processing");

        await repository.AddAsync(record, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        var persisted = await repository.GetByOperationAndKeyAsync(
            "CreateUser",
            "idem-processing",
            TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal(record.Id, persisted.Id);
        Assert.Equal(IdempotencyStatus.Processing, persisted.Status);
        Assert.Equal(record.RequestHash, persisted.RequestHash);
        Assert.Equal(record.ExpiresAtUtc, persisted.ExpiresAtUtc);
    }

    [Fact]
    public async Task Idempotency_repository_should_mark_record_as_completed()
    {
        await using var provider = _fixture.CreateServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIdempotencyRepository>();
        var record = CreateProcessingRecord("CreateUser", "idem-completed");
        var resourceId = Guid.NewGuid();

        await repository.AddAsync(record, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        var responseBody = /*lang=json,strict*/ """
            {"id":"user-1","email":"completed@example.com"}
            """;

        record.MarkCompleted(
            201,
            responseBody,
            resourceId,
            new DateTime(2026, 06, 26, 12, 5, 0, DateTimeKind.Utc));
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        var persisted = await repository.GetByOperationAndKeyAsync(
            "CreateUser",
            "idem-completed",
            TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal(IdempotencyStatus.Completed, persisted.Status);
        Assert.Equal(201, persisted.ResponseStatusCode);
        Assert.Equal(resourceId, persisted.ResourceId);
        Assert.Equal(new DateTime(2026, 06, 26, 12, 5, 0, DateTimeKind.Utc), persisted.CompletedAtUtc);
    }

    [Fact]
    public async Task Database_should_enforce_unique_idempotency_operation_and_key()
    {
        await using var db = _fixture.CreateDbContext();

        db.IdempotencyRecords.Add(CreateProcessingRecord("CreateUser", "idem-unique"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.IdempotencyRecords.Add(CreateProcessingRecord("CreateUser", "idem-unique"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("ux_identity_idempotency_records_operation_key", postgresException.ConstraintName);
    }

    [Fact]
    public async Task Database_should_allow_same_idempotency_key_for_different_operations()
    {
        await using var db = _fixture.CreateDbContext();

        db.IdempotencyRecords.Add(CreateProcessingRecord("CreateUser", "idem-shared"));
        db.IdempotencyRecords.Add(CreateProcessingRecord("ResetPassword", "idem-shared"));

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var records = await db.IdempotencyRecords
            .AsNoTracking()
            .Where(x => x.IdempotencyKey == "idem-shared")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, records.Count);
        Assert.Contains(records, x => x.OperationName == "CreateUser");
        Assert.Contains(records, x => x.OperationName == "ResetPassword");
    }

    [Fact]
    public async Task Idempotency_repository_should_persist_and_load_response_body_json()
    {
        await using var provider = _fixture.CreateServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIdempotencyRepository>();
        var record = CreateProcessingRecord("CreateUser", "idem-json");
        var responseBody = /*lang=json,strict*/ """
            {"id":"00000000-0000-0000-0000-000000000001","merchantId":"merchant-1","email":"json@example.com"}
            """;

        record.MarkCompleted(
            201,
            responseBody,
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            new DateTime(2026, 06, 26, 12, 5, 0, DateTimeKind.Utc));

        await repository.AddAsync(record, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        var persisted = await repository.GetByOperationAndKeyAsync(
            "CreateUser",
            "idem-json",
            TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        using var document = JsonDocument.Parse(persisted.ResponseBody!);
        Assert.Equal("merchant-1", document.RootElement.GetProperty("merchantId").GetString());
        Assert.Equal("json@example.com", document.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Idempotency_repository_should_persist_expires_at_utc()
    {
        await using var db = _fixture.CreateDbContext();
        var expiresAtUtc = new DateTime(2026, 06, 27, 12, 0, 0, DateTimeKind.Utc);
        var record = IdempotencyRecord.StartProcessing(
            "CreateUser",
            "idem-expires",
            RequestHash,
            new DateTime(2026, 06, 26, 12, 0, 0, DateTimeKind.Utc),
            expiresAtUtc);

        db.IdempotencyRecords.Add(record);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var persisted = await db.IdempotencyRecords
            .AsNoTracking()
            .SingleAsync(x => x.Id == record.Id, TestContext.Current.CancellationToken);

        Assert.Equal(expiresAtUtc, persisted.ExpiresAtUtc);
        Assert.Equal(DateTimeKind.Utc, persisted.ExpiresAtUtc.Kind);
    }

    [Fact]
    public async Task SaveChanges_should_dispatch_domain_event_after_commit()
    {
        var recorder = new DomainEventRecorder();
        await using var provider = _fixture.CreateServiceProvider(services =>
        {
            services.AddSingleton(recorder);
            services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, RecordingUserRegisteredHandler>();
        });
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = CreateUser(email: "dispatch-after-commit@example.com", keycloakUserId: "kc-dispatch-after-commit");

        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(user.DomainEvents);
        Assert.Equal(["recording"], recorder.HandlerNames);
        Assert.Contains(
            await db.Users.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken),
            persisted => persisted.Id == user.Id);
    }

    [Fact]
    public async Task SaveChanges_should_not_dispatch_domain_event_when_commit_fails()
    {
        var recorder = new DomainEventRecorder();
        await using var provider = _fixture.CreateServiceProvider(services =>
        {
            services.AddSingleton(recorder);
            services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, RecordingUserRegisteredHandler>();
        });
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        db.Users.Add(CreateUser(email: "failed-dispatch@example.com", keycloakUserId: "kc-failed-dispatch-1"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        recorder.Clear();

        db.Users.Add(CreateUser(email: "failed-dispatch@example.com", keycloakUserId: "kc-failed-dispatch-2"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Empty(recorder.HandlerNames);
    }

    [Fact]
    public async Task SaveChanges_should_dispatch_same_domain_event_to_two_handlers_in_registration_order()
    {
        var recorder = new DomainEventRecorder();
        await using var provider = _fixture.CreateServiceProvider(services =>
        {
            services.AddSingleton(recorder);
            services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, FirstUserRegisteredHandler>();
            services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, SecondUserRegisteredHandler>();
        });
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        db.Users.Add(CreateUser(email: "two-handlers@example.com", keycloakUserId: "kc-two-handlers"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], recorder.HandlerNames);
    }

    [Fact]
    public async Task SaveChanges_should_log_handler_failure_without_rolling_back_commit()
    {
        var logSink = new TestLogSink();
        await using var provider = _fixture.CreateServiceProvider(services =>
        {
            services.AddLogging(builder => builder.AddProvider(new TestLoggerProvider(logSink)));
            services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, ThrowingUserRegisteredHandler>();
        });
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = CreateUser(email: "handler-failure@example.com", keycloakUserId: "kc-handler-failure");

        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(
            await db.Users.AsNoTracking().AnyAsync(x => x.Id == user.Id, TestContext.Current.CancellationToken));
        Assert.Contains(logSink.Entries, entry =>
            entry.LogLevel == LogLevel.Error &&
            entry.Message.Contains("Domain event handler", StringComparison.Ordinal));
    }

    private static User CreateUser(string email, string keycloakUserId)
        => User.Register(
            UserId.New(),
            new Email(email),
            new Username("identity.user"),
            new MerchantId("merchant-shared"),
            keycloakUserId,
            new DateTime(2026, 06, 26, 12, 0, 0, DateTimeKind.Utc));

    private const string RequestHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static IdempotencyRecord CreateProcessingRecord(string operationName, string idempotencyKey)
        => IdempotencyRecord.StartProcessing(
            operationName,
            idempotencyKey,
            RequestHash,
            new DateTime(2026, 06, 26, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 06, 27, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 06, 26, 12, 1, 0, DateTimeKind.Utc));

    private sealed class DomainEventRecorder
    {
        public List<string> HandlerNames
        {
            get;
        } = [];

        public void Record(string handlerName) => HandlerNames.Add(handlerName);

        public void Clear() => HandlerNames.Clear();
    }

    private sealed class RecordingUserRegisteredHandler(DomainEventRecorder recorder)
        : IDomainEventHandler<UserRegisteredDomainEvent>
    {
        public Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            recorder.Record("recording");
            return Task.CompletedTask;
        }
    }

    private sealed class FirstUserRegisteredHandler(DomainEventRecorder recorder)
        : IDomainEventHandler<UserRegisteredDomainEvent>
    {
        public Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            recorder.Record("first");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondUserRegisteredHandler(DomainEventRecorder recorder)
        : IDomainEventHandler<UserRegisteredDomainEvent>
    {
        public Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            recorder.Record("second");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingUserRegisteredHandler : IDomainEventHandler<UserRegisteredDomainEvent>
    {
        public Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler failed");
    }

    private sealed class TestLogSink
    {
        public List<TestLogEntry> Entries
        {
            get;
        } = [];
    }

    private sealed record TestLogEntry(LogLevel LogLevel, string Message);

    private sealed class TestLoggerProvider(TestLogSink sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestLogger(sink);

        public void Dispose()
        {
        }
    }

    private sealed class TestLogger(TestLogSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            sink.Entries.Add(new TestLogEntry(logLevel, formatter(state, exception)));
        }
    }
}
