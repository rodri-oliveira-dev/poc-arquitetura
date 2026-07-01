namespace BalanceService.Application.Contracts.Events;

public sealed record EventContractValidationResult(
    bool IsValid,
    EventContractValidationErrorCode ErrorCode,
    string? ErrorMessage,
    string? EventName,
    string? EventVersion)
{
    public static EventContractValidationResult Success(string eventName, string eventVersion)
        => new(true, EventContractValidationErrorCode.None, null, eventName, eventVersion);

    public static EventContractValidationResult Failure(
        EventContractValidationErrorCode errorCode,
        string errorMessage,
        string? eventName,
        string? eventVersion)
        => new(false, errorCode, errorMessage, eventName, eventVersion);
}
