using IdentityService.Application.Common.Exceptions;
using IdentityService.Application.Users.Commands;
using IdentityService.Application.Users.Ports;
using IdentityService.Domain.Users;

using Microsoft.Extensions.Logging;

namespace IdentityService.UnitTests.Application.Users.Commands;

public sealed class CreateUserCommandHandlerTests
{
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
    public async Task Handle_should_not_compensate_when_save_is_canceled_Async()
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

        Assert.Empty(fixture.IdentityProvider.DeletedUserIds);
    }

    private static CreateUserCommand CreateCommand(string password = "N3ver-save-me!")
        => new(
            Name: "User Name",
            Email: "user@example.com",
            Username: "user-name",
            Password: password,
            Document: "12345678900");

    private sealed class HandlerFixture
    {
        public HandlerFixture(string merchantId = "merchant-generated")
        {
            MerchantIds = new StubMerchantIdGenerator(merchantId);
            Handler = new CreateUserCommandHandler(IdentityProvider, Repository, MerchantIds, Logger);
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

        public CancellationToken CreateCancellationToken
        {
            get;
            private set;
        }

        public Task<CreateIdentityProviderUserResult> CreateUserAsync(
            CreateIdentityProviderUserRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateCancellationToken = cancellationToken;

            if (CreateException is not null)
                return Task.FromException<CreateIdentityProviderUserResult>(CreateException);

            CreateRequests.Add(request);
            return Task.FromResult(new CreateIdentityProviderUserResult("keycloak-user-1"));
        }

        public Task DeleteUserAsync(string keycloakUserId, CancellationToken cancellationToken = default)
        {
            DeletedUserIds.Add(keycloakUserId);
            return DeleteException is null
                ? Task.CompletedTask
                : Task.FromException(DeleteException);
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

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            AddCancellationToken = cancellationToken;
            AddWasCalled = true;
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
        public string Generate() => merchantId;
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
