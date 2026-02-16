namespace BalanceService.Application.Abstractions.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
