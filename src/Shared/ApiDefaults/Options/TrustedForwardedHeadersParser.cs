using System.Net;

namespace ApiDefaults.Options;

internal static class TrustedForwardedHeadersParser
{
    public static bool TryParseIpAddress(string value, out IPAddress address)
        => IPAddress.TryParse(value, out address!);

    public static bool TryParseCidr(string value, out IPNetwork network, out string? error)
    {
        network = default;
        error = null;

        string[] parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out IPAddress? prefixAddress))
        {
            error = "CIDR must use the address/prefix format.";
            return false;
        }

        if (!int.TryParse(parts[1], out int prefixLength))
        {
            error = "CIDR prefix length must be an integer.";
            return false;
        }

        int maxPrefix = prefixAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefix)
        {
            error = $"CIDR prefix length must be between 0 and {maxPrefix}.";
            return false;
        }

        if (!IPNetwork.TryParse(value, out network))
        {
            error = "CIDR must be parseable as an IP network.";
            return false;
        }

        return true;
    }
}
