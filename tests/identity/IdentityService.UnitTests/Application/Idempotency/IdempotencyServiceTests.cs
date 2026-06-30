using IdentityService.Application.Idempotency;
using IdentityService.Application.Idempotency.Ports;
using IdentityService.Application.Users.Commands;

namespace IdentityService.UnitTests.Application.Idempotency;

public sealed class IdempotencyServiceTests
{
    private const string RequestHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset _now = new(2026, 06, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_should_execute_action_and_mark_completed_when_key_is_new()
    {
        var fixture = new Fixture();
        var executed = false;

        var result = await fixture.Service.ExecuteAsync(CreateRequest(async _ =>
        {
            executed = true;
            await Task.Yield();
            return new TestResponse(Guid.NewGuid(), "created");
        }), TestContext.Current.CancellationToken);

        var record = Assert.Single(fixture.Repository.Records);
        Assert.True(executed);
        Assert.True(result.OperationExecutedNow);
        Assert.Equal(IdempotentOperationResultKind.ExecutedNow, result.Kind);
        Assert.Equal(IdempotencyStatus.Completed, record.Status);
        Assert.Equal(201, record.ResponseStatusCode);
        Assert.NotNull(record.ResponseBody);
        Assert.Equal(2, fixture.Repository.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_should_replay_completed_response_for_same_key_and_hash()
    {
        var fixture = new Fixture();
        var storedResponse = new TestResponse(Guid.NewGuid(), "created");
        fixture.Repository.SeedCompleted(storedResponse);
        var executions = 0;

        var result = await fixture.Service.ExecuteAsync(CreateRequest(_ =>
        {
            executions++;
            return Task.FromResult(new TestResponse(Guid.NewGuid(), "new"));
        }), TestContext.Current.CancellationToken);

        Assert.Equal(0, executions);
        Assert.True(result.ResponseRecoveredFromPreviousExecution);
        Assert.Equal(201, result.ResponseStatusCode);
        Assert.Equal(storedResponse, result.Response);
        Assert.Equal(0, fixture.Repository.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_should_return_conflict_for_same_key_and_different_hash()
    {
        var fixture = new Fixture();
        fixture.Repository.SeedCompleted(new TestResponse(Guid.NewGuid(), "created"));

        var result = await fixture.Service.ExecuteAsync(CreateRequest(
            _ => Task.FromResult(new TestResponse(Guid.NewGuid(), "new")),
            requestHash: "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsConflict);
        Assert.Equal(IdempotentOperationResultKind.ConflictingPayload, result.Kind);
        Assert.Contains("different logical payload", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_should_return_in_progress_when_existing_record_is_processing()
    {
        var fixture = new Fixture();
        fixture.Repository.SeedProcessing();

        var result = await fixture.Service.ExecuteAsync(
            CreateRequest(_ => Task.FromResult(new TestResponse(Guid.NewGuid(), "new"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsInProgress);
        Assert.Equal(IdempotentOperationResultKind.InProgress, result.Kind);
        Assert.Contains("still processing", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_should_preserve_action_exception_and_mark_failed()
    {
        var fixture = new Fixture();
        var expected = new InvalidOperationException("business failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ExecuteAsync(
                CreateRequest(_ => Task.FromException<TestResponse>(expected)),
                TestContext.Current.CancellationToken));

        var record = Assert.Single(fixture.Repository.Records);
        Assert.Same(expected, exception);
        Assert.Equal(IdempotencyStatus.Failed, record.Status);
        Assert.Equal("business failed", record.ErrorMessage);
        Assert.Equal(2, fixture.Repository.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_should_recover_response_body_correctly()
    {
        var fixture = new Fixture();
        var response = new TestResponse(Guid.NewGuid(), "created");
        fixture.Repository.SeedCompleted(response);

        var result = await fixture.Service.ExecuteAsync(
            CreateRequest(_ => Task.FromResult(new TestResponse(Guid.NewGuid(), "new"))),
            TestContext.Current.CancellationToken);

        Assert.Equal(response.Id, result.Response?.Id);
        Assert.Equal(response.Status, result.Response?.Status);
    }

    [Fact]
    public async Task ExecuteAsync_should_not_execute_action_when_completed_response_exists()
    {
        var fixture = new Fixture();
        fixture.Repository.SeedCompleted(new TestResponse(Guid.NewGuid(), "created"));
        var executed = false;

        await fixture.Service.ExecuteAsync(CreateRequest(_ =>
        {
            executed = true;
            return Task.FromResult(new TestResponse(Guid.NewGuid(), "new"));
        }), TestContext.Current.CancellationToken);

        Assert.False(executed);
    }

    [Fact]
    public async Task ExecuteAsync_should_assign_expires_at_utc_from_configured_ttl()
    {
        var fixture = new Fixture();
        var ttl = TimeSpan.FromHours(24);

        await fixture.Service.ExecuteAsync(CreateRequest(
            _ => Task.FromResult(new TestResponse(Guid.NewGuid(), "created")),
            timeToLive: ttl),
            TestContext.Current.CancellationToken);

        var record = Assert.Single(fixture.Repository.Records);
        Assert.Equal(_now.UtcDateTime, record.CreatedAtUtc);
        Assert.Equal(_now.Add(ttl).UtcDateTime, record.ExpiresAtUtc);
    }

    [Fact]
    public void ComputeHash_should_ignore_create_user_password_by_using_safe_logical_payload()
    {
        var serializer = new StableJsonIdempotencyResponseSerializer();
        var hasher = new Sha256IdempotencyRequestHasher(serializer);
        var first = CreateUserIdempotencyPayload.From(CreateCommand("first-password"));
        var second = CreateUserIdempotencyPayload.From(CreateCommand("second-password"));

        Assert.Equal(hasher.ComputeHash(first), hasher.ComputeHash(second));
    }

    [Fact]
    public void Serialize_should_produce_stable_json_independent_of_property_order()
    {
        var serializer = new StableJsonIdempotencyResponseSerializer();

        var first = serializer.Serialize(new Dictionary<string, object?>
        {
            ["zeta"] = 1,
            ["alpha"] = new Dictionary<string, object?>
            {
                ["beta"] = true,
                ["alpha"] = "value"
            }
        });

        var second = serializer.Serialize(new Dictionary<string, object?>
        {
            ["alpha"] = new Dictionary<string, object?>
            {
                ["alpha"] = "value",
                ["beta"] = true
            },
            ["zeta"] = 1
        });

        Assert.Equal(first, second);
        Assert.Equal(/*lang=json,strict*/ """{"alpha":{"alpha":"value","beta":true},"zeta":1}""", first);
    }

    private static IdempotentOperationRequest<TResponse> CreateRequest<TResponse>(
        Func<CancellationToken, Task<TResponse>> action,
        string requestHash = RequestHash,
        TimeSpan? timeToLive = null)
        => new(
            operationName: CreateUserIdempotencyPayload.CreateUserOperationName,
            idempotencyKey: "idem-1",
            requestHash: requestHash,
            responseStatusCode: 201,
            timeToLive: timeToLive ?? TimeSpan.FromHours(24),
            executeAsync: action);

    private static CreateUserCommand CreateCommand(string password)
        => new(
            Name: "User Name",
            Email: "user@example.com",
            Username: "user-name",
            Password: password,
            Document: "12345678900");

    private sealed record TestResponse(Guid Id, string Status);

    private sealed class Fixture
    {
        public Fixture()
        {
            Serializer = new StableJsonIdempotencyResponseSerializer();
            Repository = new FakeIdempotencyRepository(Serializer);
            Service = new IdempotencyService(Repository, Serializer, new FakeTimeProvider(_now));
        }

        public StableJsonIdempotencyResponseSerializer Serializer
        {
            get;
        }

        public FakeIdempotencyRepository Repository
        {
            get;
        }

        public IdempotencyService Service
        {
            get;
        }
    }

    private sealed class FakeIdempotencyRepository(IIdempotencyResponseSerializer serializer) : IIdempotencyRepository
    {
        private readonly List<IdempotencyRecord> _records = [];

        public IReadOnlyList<IdempotencyRecord> Records => _records;

        public int SaveChangesCount
        {
            get;
            private set;
        }

        public Task<IdempotencyRecord?> GetByOperationAndKeyAsync(
            string operationName,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_records.FirstOrDefault(x =>
                x.OperationName == operationName && x.IdempotencyKey == idempotencyKey));

        public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
        {
            _records.Add(record);
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public void SeedProcessing()
        {
            _records.Add(CreateProcessingRecord());
        }

        public void SeedCompleted(TestResponse response)
        {
            var record = CreateProcessingRecord();
            record.MarkCompleted(201, serializer.Serialize(response), response.Id, _now.AddMinutes(1).UtcDateTime);
            _records.Add(record);
        }

        private static IdempotencyRecord CreateProcessingRecord()
            => IdempotencyRecord.StartProcessing(
                CreateUserIdempotencyPayload.CreateUserOperationName,
                "idem-1",
                RequestHash,
                _now.UtcDateTime,
                _now.AddHours(24).UtcDateTime);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
