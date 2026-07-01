namespace AuditService.Application.Abstractions.Persistence;

public sealed class SourceEventIdUniqueConstraintViolationException : Exception
{
    public SourceEventIdUniqueConstraintViolationException()
        : base("SourceEventId already exists.")
    {
    }

    public SourceEventIdUniqueConstraintViolationException(Exception innerException)
        : base("SourceEventId already exists.", innerException)
    {
    }
}
