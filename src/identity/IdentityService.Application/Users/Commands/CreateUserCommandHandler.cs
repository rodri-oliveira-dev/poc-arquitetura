using IdentityService.Application.Common.Exceptions;
using IdentityService.Application.Idempotency;
using IdentityService.Application.Users.Ports;
using IdentityService.Domain.Users;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Users.Commands;

public sealed partial class CreateUserCommandHandler(
    IIdentityProviderUserService identityProvider,
    IUserRepository users,
    IMerchantIdGenerator merchantIdGenerator,
    IIdempotencyService idempotencyService,
    IIdempotencyRequestHasher idempotencyRequestHasher,
    IOptions<CreateUserConsistencyOptions> consistencyOptions,
    ILogger<CreateUserCommandHandler> logger)
{
    private const int CreatedStatusCode = 201;
    private static readonly TimeSpan _idempotencyTimeToLive = TimeSpan.FromHours(24);
    private static readonly TimeSpan _idempotencyProcessingLockDuration = TimeSpan.FromMinutes(10);

    public async Task<CreateUserResult> Handle(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            return await ExecuteCreateAsync(command, cancellationToken);

        var requestHash = idempotencyRequestHasher.ComputeHash(CreateUserIdempotencyPayload.From(command));

        LogIdempotentCreateUserRequested(logger, CreateUserIdempotencyPayload.CreateUserOperationName);

        var executionState = CreateUserExecutionState.NotStarted();
        var idempotentResult = await idempotencyService.ExecuteAsync(
            new IdempotentOperationRequest<CreateUserResult>(
                CreateUserIdempotencyPayload.CreateUserOperationName,
                command.IdempotencyKey,
                requestHash,
                CreatedStatusCode,
                _idempotencyTimeToLive,
                executeAsync: token => ExecuteCreateAsync(
                    command,
                    saveChanges: false,
                    executionState,
                    token),
                resourceIdSelector: response => response.Id,
                processingLockDuration: _idempotencyProcessingLockDuration,
                onPersistenceFailureAsync: (exception, token) => CompensateIdentityProviderAsync(
                    executionState,
                    exception,
                    token)),
            cancellationToken);

        if (idempotentResult.Response is not null)
        {
            if (idempotentResult.ResponseRecoveredFromPreviousExecution)
                LogIdempotentCreateUserReplayed(logger, CreateUserIdempotencyPayload.CreateUserOperationName);

            return idempotentResult.Response;
        }

        if (idempotentResult.IsConflict)
        {
            throw new IdempotencyConflictException(
                "Idempotency key conflict",
                idempotentResult.ErrorMessage ?? "Idempotency key already used with a different logical payload.");
        }

        if (idempotentResult.IsInProgress)
        {
            throw new IdempotencyConflictException(
                "Idempotency key is still processing",
                idempotentResult.ErrorMessage ?? "Idempotency key is still processing.");
        }

        throw new InvalidOperationException($"Unsupported idempotency result '{idempotentResult.Kind}'.");
    }

    private async Task<CreateUserResult> ExecuteCreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
        => await ExecuteCreateAsync(
            command,
            saveChanges: true,
            CreateUserExecutionState.NotStarted(),
            cancellationToken);

    private async Task<CreateUserResult> ExecuteCreateAsync(
        CreateUserCommand command,
        bool saveChanges,
        CreateUserExecutionState executionState,
        CancellationToken cancellationToken)
    {
        var identityUser = await identityProvider.CreateUserAsync(
            new CreateIdentityProviderUserRequest(
                command.Name,
                command.Email,
                command.Username,
                command.Password),
            cancellationToken);
        executionState.MarkIdentityProviderUserCreated(identityUser.KeycloakUserId);

        User user;
        try
        {
            user = User.Register(
                UserId.New(),
                new Email(command.Email),
                new Username(command.Username),
                new MerchantId(merchantIdGenerator.Generate()),
                identityUser.KeycloakUserId);

            executionState.MarkLocalPersistenceStarted();
            await users.AddAsync(user, cancellationToken);

            if (saveChanges)
            {
                await users.SaveChangesAsync(cancellationToken);
                executionState.MarkLocalPersistenceConfirmed();
            }
        }
        catch (Exception exception)
        {
            _ = await CompensateIdentityProviderAsync(
                executionState,
                exception,
                CancellationToken.None);

            throw;
        }

        return new CreateUserResult(
            user.Id.Value,
            user.KeycloakUserId,
            user.MerchantId.Value,
            user.Username.Value,
            user.Email.Value);
    }

    private async Task<string?> CompensateIdentityProviderAsync(
        CreateUserExecutionState executionState,
        Exception originalException,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalException);
        ArgumentNullException.ThrowIfNull(executionState);

        if (!executionState.IdentityProviderUserCreated)
            return IdempotencyFailureStage.BeforeExternalSideEffect;

        if (executionState.LocalPersistenceConfirmed)
            return IdempotencyFailureStage.AfterLocalPersistenceConfirmed;

        var keycloakUserId = executionState.KeycloakUserId!;
        using var compensationTimeout = CreateCompensationTimeout(cancellationToken);

        try
        {
            await identityProvider.DeleteUserAsync(keycloakUserId, compensationTimeout.Token);
            IdempotencyFailureMetadata.SetFailureStage(
                originalException,
                IdempotencyFailureStage.AfterIdentityProviderCompensated);

            return IdempotencyFailureStage.AfterIdentityProviderCompensated;
        }
#pragma warning disable CA1031 // Compensation failure is logged without hiding the original persistence failure.
        catch (Exception compensationException)
#pragma warning restore CA1031
        {
            IdempotencyFailureMetadata.SetFailureStage(
                originalException,
                IdempotencyFailureStage.AfterIdentityProviderCompensationFailed);
            LogIdentityProviderUserCompensationFailed(
                logger,
                compensationException,
                keycloakUserId,
                originalException.GetType().Name);

            return IdempotencyFailureStage.AfterIdentityProviderCompensationFailed;
        }
    }

    private CancellationTokenSource CreateCompensationTimeout(CancellationToken cancellationToken)
    {
        var timeout = consistencyOptions.Value.CompensationTimeout;
        if (timeout <= TimeSpan.Zero)
            timeout = new CreateUserConsistencyOptions().CompensationTimeout;

        var source = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new CancellationTokenSource();
        source.CancelAfter(timeout);

        return source;
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Cadastro de usuario com idempotencia solicitado. OperationName: {OperationName}")]
    private static partial void LogIdempotentCreateUserRequested(
        ILogger logger,
        string operationName);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Cadastro de usuario recuperado por idempotencia. OperationName: {OperationName}")]
    private static partial void LogIdempotentCreateUserReplayed(
        ILogger logger,
        string operationName);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Falha ao compensar usuario criado no provedor de identidade. KeycloakUserId: {KeycloakUserId}; OriginalExceptionType: {OriginalExceptionType}")]
    private static partial void LogIdentityProviderUserCompensationFailed(
        ILogger logger,
        Exception exception,
        string keycloakUserId,
        string originalExceptionType);

    private sealed class CreateUserExecutionState
    {
        private CreateUserExecutionState()
        {
        }

        public string? KeycloakUserId
        {
            get; private set;
        }

        public bool IdentityProviderUserCreated => !string.IsNullOrWhiteSpace(KeycloakUserId);

        public bool LocalPersistenceStarted
        {
            get; private set;
        }

        public bool LocalPersistenceConfirmed
        {
            get; private set;
        }

        public static CreateUserExecutionState NotStarted() => new();

        public void MarkIdentityProviderUserCreated(string keycloakUserId)
        {
            if (string.IsNullOrWhiteSpace(keycloakUserId))
                throw new ArgumentException("KeycloakUserId is required.", nameof(keycloakUserId));

            KeycloakUserId = keycloakUserId;
        }

        public void MarkLocalPersistenceStarted()
            => LocalPersistenceStarted = true;

        public void MarkLocalPersistenceConfirmed()
            => LocalPersistenceConfirmed = true;
    }
}
