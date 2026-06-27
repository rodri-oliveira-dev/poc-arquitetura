using IdentityService.Application.Users.Ports;
using IdentityService.Domain.Users;

using Microsoft.Extensions.Logging;

namespace IdentityService.Application.Users.Commands;

public sealed partial class CreateUserCommandHandler(
    IIdentityProviderUserService identityProvider,
    IUserRepository users,
    IMerchantIdGenerator merchantIdGenerator,
    ILogger<CreateUserCommandHandler> logger)
{
    public async Task<CreateUserResult> Handle(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

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
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Falha ao compensar usuario criado no provedor de identidade. KeycloakUserId: {KeycloakUserId}; OriginalExceptionType: {OriginalExceptionType}")]
    private static partial void LogIdentityProviderUserCompensationFailed(
        ILogger logger,
        Exception exception,
        string keycloakUserId,
        string originalExceptionType);
}
