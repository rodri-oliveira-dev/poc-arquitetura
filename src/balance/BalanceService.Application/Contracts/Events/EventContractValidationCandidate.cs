namespace BalanceService.Application.Contracts.Events;

public sealed record EventContractValidationCandidate(
    string? EventName,
    string? EventVersion,
    string Payload,
    IReadOnlyDictionary<string, string>? Metadata = null);
