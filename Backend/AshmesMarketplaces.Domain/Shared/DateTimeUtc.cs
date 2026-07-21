namespace AshmesMarketplaces.Domain.Shared;

public static class DateTimeUtc
{
    // Persisted DateTime values are UTC-only to match PostgreSQL timestamptz semantics.
    public static bool IsUtc(DateTime value) => value.Kind == DateTimeKind.Utc;

    public static bool IsUtc(DateTime? value) => !value.HasValue || IsUtc(value.Value);

    public static void EnsureUtc(DateTime value, string paramName)
    {
        if (!IsUtc(value))
            throw new ArgumentException("DateTime value must be UTC", paramName);
    }

    public static void EnsureUtc(DateTime? value, string paramName)
    {
        if (value.HasValue)
            EnsureUtc(value.Value, paramName);
    }
}
