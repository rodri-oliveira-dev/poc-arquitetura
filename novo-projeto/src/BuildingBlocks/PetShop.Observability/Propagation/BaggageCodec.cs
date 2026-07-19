using System.Diagnostics;

namespace PetShop.Observability.Propagation;

internal static class BaggageCodec
{
    public static string? Format(IEnumerable<KeyValuePair<string, string?>>? baggage)
    {
        if (baggage is null)
        {
            return null;
        }

        var members = baggage
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}")
            .ToArray();

        return members.Length == 0
            ? null
            : string.Join(',', members);
    }

    public static void Apply(Activity? activity, string? baggage)
    {
        if (activity is null || string.IsNullOrWhiteSpace(baggage))
        {
            return;
        }

        foreach (string member in baggage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string pair = member.Split(';', 2, StringSplitOptions.TrimEntries)[0];
            int separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == pair.Length - 1)
            {
                continue;
            }

            try
            {
                string key = Uri.UnescapeDataString(pair[..separatorIndex]);
                string value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    activity.SetBaggage(key, value);
                }
            }
            catch (UriFormatException)
            {
                // Ignora apenas o membro inválido; os demais itens de baggage continuam válidos.
            }
        }
    }
}
