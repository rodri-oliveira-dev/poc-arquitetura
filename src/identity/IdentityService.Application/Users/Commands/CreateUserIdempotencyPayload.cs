namespace IdentityService.Application.Users.Commands;

public sealed record CreateUserIdempotencyPayload(
    string OperationName,
    string Username,
    string Name,
    string Email,
    string? Document)
{
    public const string CreateUserOperationName = "CreateUser";

    public static CreateUserIdempotencyPayload From(CreateUserCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return new CreateUserIdempotencyPayload(
            CreateUserOperationName,
            command.Username,
            command.Name,
            command.Email,
            command.Document);
    }
}
