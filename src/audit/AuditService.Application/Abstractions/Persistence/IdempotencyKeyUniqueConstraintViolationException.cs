namespace AuditService.Application.Abstractions.Persistence;

public sealed class IdempotencyKeyUniqueConstraintViolationException : Exception
{
    public IdempotencyKeyUniqueConstraintViolationException()
        : base("Idempotency-Key already exists.")
    {
    }

    public IdempotencyKeyUniqueConstraintViolationException(Exception innerException)
        : base("Idempotency-Key already exists.", innerException)
    {
    }
}
