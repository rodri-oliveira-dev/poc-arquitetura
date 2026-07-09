namespace PaymentService.Application.Abstractions.Time;

public interface IClock
{
    DateTimeOffset UtcNow
    {
        get;
    }
}
