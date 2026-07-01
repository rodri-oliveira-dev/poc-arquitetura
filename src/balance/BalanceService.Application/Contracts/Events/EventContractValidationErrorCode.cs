namespace BalanceService.Application.Contracts.Events;

public enum EventContractValidationErrorCode
{
    None = 0,
    EventNameMissing,
    EventVersionMissing,
    SchemaNotFound,
    UnsupportedVersion,
    InvalidJson,
    InvalidPayload
}
