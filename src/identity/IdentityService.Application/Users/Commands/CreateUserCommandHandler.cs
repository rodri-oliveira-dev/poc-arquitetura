using IdentityService.Application.Common.Exceptions;
using IdentityService.Application.Idempotency;
using IdentityService.Application.Users.Ports;
using IdentityService.Domain.Users;

using Microsoft.Extensions.Logging;

namespace IdentityService.Application.Users.Commands;

public sealed partial class CreateUserCommandHandler(
    IIdentityProviderUserService identityProvider,
    IUserRepository users,
    IMerchantIdGenerator merchantIdGenerator,
    IIdempotencyService idempotencyService,
    IIdempotencyRequestHasher idempotencyRequestHasher,
    ILogger<CreateUserCommandHandler> logger)
{
    private const int CreatedStatusCode = 201;
    private static readonly TimeSpan _idempotencyTimeToLive = TimeSpan.FromHours(24);

    public async Task<CreateUserResult> Handle(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            return await ExecuteCreateAsync(command, cancellationToken);

        var requestHash = idempotencyRequestHasher.ComputeHash(CreateUserIdempotencyPayload.From(command));

        LogIdempotentCreateUserRequested(logger, CreateUserIdempotencyPayload.CreateUserOperationName);

        var idempotentResult = await idempotencyService.ExecuteAsync(
            new IdempotentOperationRequest<CreateUserResult>(
                CreateUserIdempotencyPayload.CreateUserOperationName,
                command.IdempotencyKey,
                requestHash,
                CreatedStatusCode,
                _idempotencyTimeToLive,
                executeAsync: token => ExecuteCreateAsync(command, token),
                resourceIdSelector: response => response.Id),
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
    {
        var identityUser = await identityProvider.CreateUserAsync(
            new CreateIdentityProviderUserRequest(
                command.Name,
                command.Email,
                command.Username,
                command.Password),
            cancellationToken);

        var user = User.Register(
            UserId.New(),
            new Email(command.Email),
            new Username(command.Username),
            new MerchantId(merchantIdGenerator.Generate()),
            identityUser.KeycloakUserId);

        try
        {
            await users.AddAsync(user, cancellationToken);
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await identityProvider.DeleteUserAsync(identityUser.KeycloakUserId, CancellationToken.None);
            }
#pragma warning disable CA1031 // Compensation failure is logged without hiding the original persistence failure.
            catch (Exception compensationException)
#pragma warning restore CA1031
            {
                LogIdentityProviderUserCompensationFailed(
                    logger,
                    compensationException,
                    identityUser.KeycloakUserId,
                    exception.GetType().Name);
            }

            throw;
        }

        return new CreateUserResult(
            user.Id.Value,
            user.KeycloakUserId,
            user.MerchantId.Value,
            user.Username.Value,
            user.Email.Value);
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
}
