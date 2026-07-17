using IdentityService.Application.Common.Exceptions;
using IdentityService.Application.Idempotency;
using IdentityService.Application.Idempotency.Ports;
using IdentityService.Application.Users.Commands;
using IdentityService.Application.Users.Ports;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.Users;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IdentityService.UnitTests.Application.Users.Commands;

public sealed class CreateUserCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 06, 26, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Handle_should_create_user_in_keycloak_then_save_user_Async()
    {
        var fixture = new HandlerFixture();

        var result = await fixture.Handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal("keycloak-user-1", result.KeycloakUserId);
        Assert.Equal("merchant-generated", result.MerchantId);
        Assert.Equal("user-name", result.Username);
        Assert.Equal("user@example.com", result.Email);
        Assert.NotEqual(Guid.Empty, result.Id);

        Assert.Single(fixture.IdentityProvider.CreateRequests);
        Assert.Equal("User Name", fixture.IdentityProvider.CreateRequests[0].Name);
        Assert.NotNull(fixture.Repository.SavedUser);
        Assert.Equal(fixture.Repository.SavedUser.Id.Value, result.Id);
        Assert.True(fixture.Repository.AddCalledBeforeSave);
        Assert.Empty(fixture.IdentityProvider.DeletedUserIds);

        var domainEvent = Assert.IsType<UserRegisteredDomainEvent>(
            Assert.Single(fixture.Repository.SavedUser.DomainEvents));
        Assert.Equal(fixture.Repository.SavedUser.Id, domainEvent.UserId);
        Assert.Equal(fixture.Repository.SavedUser.MerchantId, domainEvent.MerchantId);
        Assert.Equal(fixture.Repository.SavedUser.KeycloakUserId, domainEvent.KeycloakUserId);
        Assert.Equal(Now.UtcDateTime, domainEvent.OccurredAt);
    }

    [Fact]
    public async Task Handle_should_not_save_when_keycloak_fails_Async()
    {
        var fixture = new HandlerFixture
        {
            IdentityProvider =
            {
                CreateException = new IdentityProviderException(
                    IdentityProviderErrorKind.Unexpected,
                    "provider failed")
            }
        };

        await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.False(fixture.Repository.AddWasCalled);
        Assert.False(fixture.Repository.SaveWasCalled);
        Assert.Empty(fixture.IdentityProvider.DeletedUserIds);
    }

    [Fact]
    public async Task Handle_should_compensate_keycloak_when_database_save_fails_Async()
    {
        var fixture = new HandlerFixture
        {
            Repository =
            {
                SaveException = new InvalidOperationException("database failed")
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Equal("database failed", exception.Message);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
    }

    [Fact]
    public async Task Handle_should_compensate_keycloak_when_database_add_fails_Async()
    {
        var fixture = new HandlerFixture
        {
            Repository =
            {
                AddException = new InvalidOperationException("add failed")
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Equal("add failed", exception.Message);
        Assert.False(fixture.Repository.SaveWasCalled);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
    }

    [Fact]
    public async Task Handle_should_compensate_keycloak_when_merchant_generation_fails_after_keycloak_effect_Async()
    {
        var fixture = new HandlerFixture();
        fixture.MerchantIds.GenerateException = new InvalidOperationException("merchant generation failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Equal("merchant generation failed", exception.Message);
        Assert.False(fixture.Repository.AddWasCalled);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
    }

    [Fact]
    public async Task Handle_should_compensate_keycloak_when_merchant_id_is_invalid_after_keycloak_effect_Async()
    {
        var fixture = new HandlerFixture(merchantId: "");

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Equal("MerchantId is required.", exception.Message);
        Assert.False(fixture.Repository.AddWasCalled);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
    }

    [Fact]
    public async Task Handle_should_compensate_keycloak_when_email_is_invalid_after_keycloak_effect_Async()
    {
        var fixture = new HandlerFixture();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Handler.Handle(CreateCommand(email: "not-an-email"), CancellationToken.None));

        Assert.Equal("Email is invalid.", exception.Message);
        Assert.False(fixture.Repository.AddWasCalled);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
    }

    [Fact]
    public async Task Handle_should_compensate_keycloak_when_username_is_invalid_after_keycloak_effect_Async()
    {
        var fixture = new HandlerFixture();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Handler.Handle(CreateCommand(username: ""), CancellationToken.None));

        Assert.Equal("Username is required.", exception.Message);
        Assert.False(fixture.Repository.AddWasCalled);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
    }

    [Fact]
    public async Task Handle_should_log_compensation_failure_without_masking_database_failure_Async()
    {
        var fixture = new HandlerFixture
        {
            IdentityProvider =
            {
                DeleteException = new InvalidOperationException("compensation failed")
            },
            Repository =
            {
                SaveException = new InvalidOperationException("database failed")
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Equal("database failed", exception.Message);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
        Assert.Contains(fixture.Logger.Messages, message =>
            message.Contains("Falha ao compensar usuario criado no provedor de identidade", StringComparison.Ordinal));
        Assert.Contains(fixture.Logger.Messages, message =>
            message.Contains("compensation failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_should_return_generated_merchant_id_Async()
    {
        var fixture = new HandlerFixture("merchant-from-generator");

        var result = await fixture.Handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal("merchant-from-generator", result.MerchantId);
        Assert.Equal("merchant-from-generator", fixture.Repository.SavedUser?.MerchantId.Value);
    }

    [Fact]
    public async Task Handle_should_not_persist_password_locally_Async()
    {
        var fixture = new HandlerFixture();

        await fixture.Handler.Handle(CreateCommand(password: "N3ver-save-me!"), CancellationToken.None);

        Assert.NotNull(fixture.Repository.SavedUser);
        Assert.DoesNotContain(
            typeof(User).GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            "N3ver-save-me!",
            string.Join('|', fixture.Repository.PersistedScalarValues),
            StringComparison.Ordinal);
    }

    [Fact]
    public void CreateUserCommand_should_not_accept_merchant_id()
    {
        var merchantIdProperties = typeof(CreateUserCommand)
            .GetProperties()
            .Where(property => property.Name.Contains("MerchantId", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(merchantIdProperties);
    }

    [Fact]
    public async Task Handle_should_propagate_cancellation_token_to_create_add_and_save_Async()
    {
        var fixture = new HandlerFixture();
        using var cts = new CancellationTokenSource();

        await fixture.Handler.Handle(CreateCommand(), cts.Token);

        Assert.Equal(cts.Token, fixture.IdentityProvider.CreateCancellationToken);
        Assert.Equal(cts.Token, fixture.Repository.AddCancellationToken);
        Assert.Equal(cts.Token, fixture.Repository.SaveCancellationToken);
    }

    [Fact]
    public async Task Handle_should_not_compensate_when_create_is_canceled_before_keycloak_effect_Async()
    {
        var fixture = new HandlerFixture
        {
            IdentityProvider =
            {
                CreateException = new OperationCanceledException("create canceled before external effect")
            }
        };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Handler.Handle(CreateCommand(), cts.Token));

        Assert.Empty(fixture.IdentityProvider.DeletedUserIds);
        Assert.False(fixture.Repository.AddWasCalled);
    }

    [Fact]
    public async Task Handle_should_compensate_with_independent_token_when_save_is_canceled_after_keycloak_effect_Async()
    {
        var fixture = new HandlerFixture
        {
            Repository =
            {
                SaveException = new OperationCanceledException("save canceled")
            }
        };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Handler.Handle(CreateCommand(), cts.Token));

        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
        Assert.NotEqual(cts.Token, fixture.IdentityProvider.DeleteCancellationToken);
        Assert.True(fixture.IdentityProvider.DeleteCancellationToken.CanBeCanceled);
    }

    [Fact]
    public async Task Handle_should_compensate_with_independent_token_when_add_is_canceled_after_keycloak_effect_Async()
    {
        var fixture = new HandlerFixture
        {
            Repository =
            {
                AddException = new OperationCanceledException("add canceled")
            }
        };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Handler.Handle(CreateCommand(), cts.Token));

        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
        Assert.NotEqual(cts.Token, fixture.IdentityProvider.DeleteCancellationToken);
        Assert.True(fixture.IdentityProvider.DeleteCancellationToken.CanBeCanceled);
        Assert.False(fixture.Repository.SaveWasCalled);
    }

    [Fact]
    public async Task Handle_should_preserve_original_exception_when_compensation_exceeds_timeout_Async()
    {
        var fixture = new HandlerFixture(compensationTimeout: TimeSpan.FromMilliseconds(10))
        {
            IdentityProvider =
            {
                WaitForDeleteCancellation = true
            },
            Repository =
            {
                SaveException = new InvalidOperationException("database failed")
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Equal("database failed", exception.Message);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
        Assert.True(fixture.IdentityProvider.DeleteCancellationToken.IsCancellationRequested);
        Assert.Contains(fixture.Logger.Messages, message =>
            message.Contains("Falha ao compensar usuario criado no provedor de identidade", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_should_compensate_when_save_changes_is_canceled_during_idempotent_operation_Async()
    {
        var fixture = new HandlerFixture
        {
            IdempotencyRepository =
            {
                SaveChangesException = new OperationCanceledException("commit canceled")
            }
        };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Handler.Handle(CreateCommand(idempotencyKey: "idem-canceled-save-1"), cts.Token));

        var record = Assert.Single(fixture.IdempotencyRepository.Records);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
        Assert.Equal(IdempotencyStatus.Failed, record.Status);
        Assert.Equal(IdempotencyFailureStage.AfterIdentityProviderCompensated, record.FailureStage);
    }

    [Fact]
    public async Task Handle_should_execute_and_store_response_when_idempotency_key_is_new_Async()
    {
        var fixture = new HandlerFixture();

        var result = await fixture.Handler.Handle(
            CreateCommand(idempotencyKey: "idem-create-user-1"),
            CancellationToken.None);

        var record = Assert.Single(fixture.IdempotencyRepository.Records);
        Assert.Equal(IdempotencyStatus.Completed, record.Status);
        Assert.Equal(201, record.ResponseStatusCode);
        Assert.DoesNotContain("N3ver-save-me!", record.ResponseBody, StringComparison.Ordinal);
        Assert.Single(fixture.IdentityProvider.CreateRequests);
        Assert.Equal("merchant-generated", result.MerchantId);
    }

    [Fact]
    public async Task Handle_should_replay_completed_response_without_repeating_side_effects_Async()
    {
        var fixture = new HandlerFixture();
        var command = CreateCommand(idempotencyKey: "idem-replay-1");

        var first = await fixture.Handler.Handle(command, CancellationToken.None);
        var second = await fixture.Handler.Handle(
            command with
            {
                Password = "another-secret"
            },
            CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Single(fixture.IdentityProvider.CreateRequests);
        Assert.Equal(1, fixture.Repository.AddCallCount);
        Assert.Equal(1, fixture.MerchantIds.GenerateCount);
    }

    [Fact]
    public async Task Handle_should_return_conflict_for_same_key_and_different_logical_payload_Async()
    {
        var fixture = new HandlerFixture();
        await fixture.Handler.Handle(CreateCommand(idempotencyKey: "idem-conflict-1"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            fixture.Handler.Handle(
                CreateCommand(email: "another@example.com", idempotencyKey: "idem-conflict-1"),
                CancellationToken.None));

        Assert.Equal("Idempotency key conflict", exception.Title);
        Assert.Single(fixture.IdentityProvider.CreateRequests);
    }

    [Fact]
    public async Task Handle_should_return_conflict_when_key_is_still_processing_Async()
    {
        var fixture = new HandlerFixture();
        var command = CreateCommand(idempotencyKey: "idem-processing-1");
        fixture.IdempotencyRepository.SeedProcessing(
            "idem-processing-1",
            fixture.Hasher.ComputeHash(CreateUserIdempotencyPayload.From(command)));

        var exception = await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            fixture.Handler.Handle(command, CancellationToken.None));

        Assert.Equal("Idempotency key is still processing", exception.Title);
        Assert.Empty(fixture.IdentityProvider.CreateRequests);
    }

    [Fact]
    public async Task Handle_should_allow_retry_after_idempotent_failure_before_keycloak_effect_Async()
    {
        var fixture = new HandlerFixture
        {
            IdentityProvider =
            {
                CreateException = new IdentityProviderException(
                    IdentityProviderErrorKind.Unexpected,
                    "provider failed")
            }
        };
        var command = CreateCommand(idempotencyKey: "idem-provider-retry-1");

        await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Handler.Handle(command, CancellationToken.None));

        var failedRecord = Assert.Single(fixture.IdempotencyRepository.Records);
        Assert.Equal(IdempotencyStatus.Failed, failedRecord.Status);
        Assert.Equal(IdempotencyFailureStage.BeforeExternalSideEffect, failedRecord.FailureStage);

        fixture.IdentityProvider.CreateException = null;
        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.Equal("keycloak-user-1", result.KeycloakUserId);
        Assert.Equal(IdempotencyStatus.Completed, failedRecord.Status);
        Assert.Equal(2, fixture.IdentityProvider.CreateAttemptCount);
        Assert.Single(fixture.IdentityProvider.CreateRequests);
    }

    [Fact]
    public async Task Handle_should_compensate_keycloak_when_idempotent_local_commit_fails_Async()
    {
        var expected = new InvalidOperationException("database failed");
        var fixture = new HandlerFixture
        {
            IdempotencyRepository =
            {
                SaveChangesException = expected
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Handler.Handle(CreateCommand(idempotencyKey: "idem-commit-fails-1"), CancellationToken.None));

        var record = Assert.Single(fixture.IdempotencyRepository.Records);
        Assert.Same(expected, exception);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
        Assert.Equal(IdempotencyStatus.Failed, record.Status);
        Assert.Equal(IdempotencyFailureStage.AfterIdentityProviderCompensated, record.FailureStage);
        Assert.Null(record.ResponseBody);
    }

    [Fact]
    public async Task Handle_should_preserve_original_exception_when_idempotent_compensation_fails_Async()
    {
        var expected = new InvalidOperationException("database failed");
        var fixture = new HandlerFixture
        {
            IdentityProvider =
            {
                DeleteException = new InvalidOperationException("compensation failed")
            },
            IdempotencyRepository =
            {
                SaveChangesException = expected
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Handler.Handle(CreateCommand(idempotencyKey: "idem-compensation-fails-1"), CancellationToken.None));

        var record = Assert.Single(fixture.IdempotencyRepository.Records);
        Assert.Same(expected, exception);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
        Assert.Equal(IdempotencyStatus.Failed, record.Status);
        Assert.Equal(IdempotencyFailureStage.AfterIdentityProviderCompensationFailed, record.FailureStage);
        Assert.Contains(fixture.Logger.Messages, message =>
            message.Contains("Falha ao compensar usuario criado no provedor de identidade", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_should_allow_retry_after_idempotent_failure_with_confirmed_compensation_Async()
    {
        var expected = new InvalidOperationException("database failed");
        var fixture = new HandlerFixture
        {
            IdempotencyRepository =
            {
                SaveChangesException = expected
            }
        };
        var command = CreateCommand(idempotencyKey: "idem-compensated-retry-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Handler.Handle(command, CancellationToken.None));

        var failedRecord = Assert.Single(fixture.IdempotencyRepository.Records);
        Assert.Equal(IdempotencyStatus.Failed, failedRecord.Status);
        Assert.Equal(IdempotencyFailureStage.AfterIdentityProviderCompensated, failedRecord.FailureStage);

        fixture.IdempotencyRepository.SaveChangesException = null;
        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.Equal("keycloak-user-1", result.KeycloakUserId);
        Assert.Equal(IdempotencyStatus.Completed, failedRecord.Status);
        Assert.Equal(2, fixture.IdentityProvider.CreateAttemptCount);
        Assert.Equal(["keycloak-user-1"], fixture.IdentityProvider.DeletedUserIds);
    }

    [Fact]
    public async Task Handle_should_block_retry_after_idempotent_failure_with_failed_compensation_Async()
    {
        var fixture = new HandlerFixture
        {
            IdentityProvider =
            {
                DeleteException = new InvalidOperationException("compensation failed")
            },
            IdempotencyRepository =
            {
                SaveChangesException = new InvalidOperationException("database failed")
            }
        };
        var command = CreateCommand(idempotencyKey: "idem-compensation-blocks-retry-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Handler.Handle(command, CancellationToken.None));

        fixture.IdempotencyRepository.SaveChangesException = null;
        var exception = await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            fixture.Handler.Handle(command, CancellationToken.None));

        Assert.Equal("Idempotency key is still processing", exception.Title);
        Assert.Equal(1, fixture.IdentityProvider.CreateAttemptCount);
    }

    [Fact]
    public async Task Handle_should_not_compensate_when_local_operation_is_confirmed_Async()
    {
        var fixture = new HandlerFixture();

        var result = await fixture.Handler.Handle(CreateCommand(idempotencyKey: "idem-confirmed-1"), CancellationToken.None);

        var record = Assert.Single(fixture.IdempotencyRepository.Records);
        Assert.Equal("keycloak-user-1", result.KeycloakUserId);
        Assert.Equal(IdempotencyStatus.Completed, record.Status);
        Assert.Empty(fixture.IdentityProvider.DeletedUserIds);
    }

    [Fact]
    public async Task Handle_should_retry_after_expired_processing_record_without_creating_user_twice_concurrently_Async()
    {
        var fixture = new HandlerFixture();
        var command = CreateCommand(idempotencyKey: "idem-expired-processing-1");
        fixture.IdempotencyRepository.SeedProcessing(
            "idem-expired-processing-1",
            fixture.Hasher.ComputeHash(CreateUserIdempotencyPayload.From(command)),
            expiresAtUtc: Now.UtcDateTime.AddMinutes(-1));

        var first = await fixture.Handler.Handle(command, CancellationToken.None);
        var second = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Single(fixture.IdentityProvider.CreateRequests);
        Assert.Equal(1, fixture.Repository.AddCallCount);
    }

    private static CreateUserCommand CreateCommand(
        string email = "user@example.com",
        string username = "user-name",
        string password = "N3ver-save-me!",
        string? idempotencyKey = null)
        => new(
            Name: "User Name",
            Email: email,
            Username: username,
            Password: password,
            Document: "12345678900",
            IdempotencyKey: idempotencyKey);

    private sealed class HandlerFixture
    {
        public HandlerFixture(
            string merchantId = "merchant-generated",
            TimeSpan? compensationTimeout = null)
        {
            MerchantIds = new StubMerchantIdGenerator(merchantId);
            Serializer = new StableJsonIdempotencyResponseSerializer();
            Hasher = new Sha256IdempotencyRequestHasher(Serializer);
            IdempotencyRepository = new FakeIdempotencyRepository();
            IdempotencyService = new IdempotencyService(
                IdempotencyRepository,
                Serializer,
                new FixedClock(Now),
                NullLogger<IdempotencyService>.Instance);
            Handler = new CreateUserCommandHandler(
                new CreateUserCommandHandlerDependencies(
                    IdentityProvider,
                    Repository,
                    MerchantIds,
                    new FixedClock(Now)),
                IdempotencyService,
                Hasher,
                Options.Create(new CreateUserConsistencyOptions
                {
                    CompensationTimeout = compensationTimeout ?? TimeSpan.FromSeconds(1)
                }),
                Logger);
        }

        public FakeIdentityProvider IdentityProvider
        {
            get;
            init;
        } = new();

        public FakeUserRepository Repository
        {
            get;
            init;
        } = new();

        public StubMerchantIdGenerator MerchantIds
        {
            get;
        }

        public StableJsonIdempotencyResponseSerializer Serializer
        {
            get;
        }

        public Sha256IdempotencyRequestHasher Hasher
        {
            get;
        }

        public FakeIdempotencyRepository IdempotencyRepository
        {
            get;
        }

        public IdempotencyService IdempotencyService
        {
            get;
        }

        public CapturingLogger<CreateUserCommandHandler> Logger
        {
            get;
        } = new();

        public CreateUserCommandHandler Handler
        {
            get;
        }
    }

    private sealed class FakeIdentityProvider : IIdentityProviderUserService
    {
        public List<CreateIdentityProviderUserRequest> CreateRequests
        {
            get;
        } = [];

        public List<string> DeletedUserIds
        {
            get;
        } = [];

        public Exception? CreateException
        {
            get;
            set;
        }

        public Exception? DeleteException
        {
            get;
            set;
        }

        public bool WaitForDeleteCancellation
        {
            get;
            set;
        }

        public CancellationToken CreateCancellationToken
        {
            get;
            private set;
        }

        public CancellationToken DeleteCancellationToken
        {
            get;
            private set;
        }

        public int CreateAttemptCount
        {
            get;
            private set;
        }

        public Task<CreateIdentityProviderUserResult> CreateUserAsync(
            CreateIdentityProviderUserRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateCancellationToken = cancellationToken;
            CreateAttemptCount++;

            if (CreateException is not null)
                return Task.FromException<CreateIdentityProviderUserResult>(CreateException);

            CreateRequests.Add(request);
            return Task.FromResult(new CreateIdentityProviderUserResult("keycloak-user-1"));
        }

        public async Task DeleteUserAsync(string keycloakUserId, CancellationToken cancellationToken = default)
        {
            DeleteCancellationToken = cancellationToken;
            DeletedUserIds.Add(keycloakUserId);

            if (WaitForDeleteCancellation)
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            if (DeleteException is not null)
                throw DeleteException;
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? SavedUser
        {
            get;
            private set;
        }

        public bool AddWasCalled
        {
            get;
            private set;
        }

        public bool SaveWasCalled
        {
            get;
            private set;
        }

        public bool AddCalledBeforeSave
        {
            get;
            private set;
        }

        public Exception? SaveException
        {
            get;
            set;
        }

        public Exception? AddException
        {
            get;
            set;
        }

        public CancellationToken AddCancellationToken
        {
            get;
            private set;
        }

        public CancellationToken SaveCancellationToken
        {
            get;
            private set;
        }

        public List<string> PersistedScalarValues
        {
            get;
        } = [];

        public int AddCallCount
        {
            get;
            private set;
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            AddCancellationToken = cancellationToken;
            AddWasCalled = true;
            AddCallCount++;
            SavedUser = user;
            PersistedScalarValues.AddRange(
            [
                user.Id.Value.ToString(),
                user.Email.Value,
                user.Username.Value,
                user.MerchantId.Value,
                user.KeycloakUserId
            ]);

            return AddException is null
                ? Task.CompletedTask
                : Task.FromException(AddException);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCancellationToken = cancellationToken;
            AddCalledBeforeSave = AddWasCalled;
            SaveWasCalled = true;

            return SaveException is null
                ? Task.FromResult(1)
                : Task.FromException<int>(SaveException);
        }
    }

    private sealed class StubMerchantIdGenerator(string merchantId) : IMerchantIdGenerator
    {
        public int GenerateCount
        {
            get;
            private set;
        }

        public Exception? GenerateException
        {
            get;
            set;
        }

        public string Generate()
        {
            GenerateCount++;
            return GenerateException is null
                ? merchantId
                : throw GenerateException;
        }
    }

    private sealed class FakeIdempotencyRepository : IIdempotencyRepository
    {
        private readonly List<IdempotencyRecord> _records = [];

        public IReadOnlyList<IdempotencyRecord> Records => _records;

        public Task<IdempotencyRecord?> GetByOperationAndKeyAsync(
            string operationName,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_records.FirstOrDefault(x =>
                x.OperationName == operationName && x.IdempotencyKey == idempotencyKey));

        public Task<bool> TryAddProcessingAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
        {
            if (_records.Any(x => x.OperationName == record.OperationName && x.IdempotencyKey == record.IdempotencyKey))
                return Task.FromResult(false);

            _records.Add(record);
            return Task.FromResult(true);
        }

        public Task<IdempotencyRecord?> TryClaimExpiredForProcessingAsync(
            string operationName,
            string idempotencyKey,
            string requestHash,
            DateTime nowUtc,
            DateTime expiresAtUtc,
            DateTime? lockedUntilUtc,
            CancellationToken cancellationToken = default)
        {
            var record = _records.FirstOrDefault(x =>
                x.OperationName == operationName &&
                x.IdempotencyKey == idempotencyKey &&
                x.ExpiresAtUtc <= nowUtc);

            if (record is null)
                return Task.FromResult<IdempotencyRecord?>(null);

            record.RestartExpiredProcessing(requestHash, nowUtc, expiresAtUtc, lockedUntilUtc);
            return Task.FromResult<IdempotencyRecord?>(record);
        }

        public Task<IdempotencyRecord?> TryClaimFailedForRetryAsync(
            string operationName,
            string idempotencyKey,
            string requestHash,
            DateTime nowUtc,
            DateTime? lockedUntilUtc,
            CancellationToken cancellationToken = default)
        {
            var record = _records.FirstOrDefault(x =>
                x.OperationName == operationName &&
                x.IdempotencyKey == idempotencyKey &&
                x.RequestHash == requestHash &&
                x.Status == IdempotencyStatus.Failed &&
                (x.FailureStage == IdempotencyFailureStage.BeforeExternalSideEffect ||
                    x.FailureStage == IdempotencyFailureStage.AfterIdentityProviderCompensated));

            if (record is null)
                return Task.FromResult<IdempotencyRecord?>(null);

            record.RestartProcessing(nowUtc, lockedUntilUtc);
            return Task.FromResult<IdempotencyRecord?>(record);
        }

        public Exception? SaveChangesException
        {
            get;
            set;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => SaveChangesException is null
                ? Task.FromResult(1)
                : Task.FromException<int>(SaveChangesException);

        public Task<int> SaveFailureAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public void SeedProcessing(
            string idempotencyKey,
            string requestHash,
            DateTime? expiresAtUtc = null)
        {
            _records.Add(IdempotencyRecord.StartProcessing(
                CreateUserIdempotencyPayload.CreateUserOperationName,
                idempotencyKey,
                requestHash,
                Now.UtcDateTime,
                expiresAtUtc ?? Now.UtcDateTime.AddHours(24)));
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages
        {
            get;
        } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));

            if (exception is not null)
                Messages.Add(exception.ToString());
        }
    }
}
