using Json.Schema;

namespace BalanceService.Application.Contracts.Events;

public interface IEventContractSchemaCatalog
{
    bool ContainsEventName(string eventName);

    bool TryGetSchema(string eventName, string eventVersion, out JsonSchema? schema);
}
