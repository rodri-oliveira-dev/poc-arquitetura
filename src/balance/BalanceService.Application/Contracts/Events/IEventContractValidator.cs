namespace BalanceService.Application.Contracts.Events;

public interface IEventContractValidator
{
    EventContractValidationResult Validate(EventContractValidationCandidate candidate);
}
