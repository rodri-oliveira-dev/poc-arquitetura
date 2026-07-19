namespace PetShop.Observability.Propagation;

internal static class CorrelationIdNormalizer
{
    public static string? Normalize(string? value)
    {
        return Guid.TryParse(value, out Guid parsed)
            ? parsed.ToString()
            : null;
    }

    public static string ResolveOrCreate(string? value)
    {
        return Normalize(value) ?? Guid.NewGuid().ToString();
    }
}
