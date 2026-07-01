namespace IdentityService.Application.Common.Exceptions;

public sealed class IdempotencyConflictException(string title, string message) : Exception(message)
{
    public string Title { get; } = title;
}
