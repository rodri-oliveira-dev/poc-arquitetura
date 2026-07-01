namespace BalanceService.Application.Contracts.Events;

public sealed record EventContractName(string EventName, string EventVersion)
{
    public static bool TryParse(string? eventType, out EventContractName? contractName)
    {
        contractName = null;

        if (string.IsNullOrWhiteSpace(eventType))
            return false;

        var parts = eventType.Split('.', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            string.IsNullOrWhiteSpace(parts[0]) ||
            string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        contractName = new EventContractName(parts[0], parts[1]);
        return true;
    }
}
