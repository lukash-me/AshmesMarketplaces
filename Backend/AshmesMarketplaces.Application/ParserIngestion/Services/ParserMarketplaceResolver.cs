using AshmesMarketplaces.Domain.Entities.Marketplaces;

namespace AshmesMarketplaces.Application.ParserIngestion.Services;

public static class ParserMarketplaceResolver
{
    public static bool IsMatch(Marketplace marketplace, string parserMarketplace)
    {
        var parserKey = Normalize(parserMarketplace);
        var marketplaceName = Normalize(marketplace.Name);
        if (parserKey.Length == 0 || marketplaceName.Length == 0)
            return false;

        if (marketplaceName == parserKey)
            return true;

        if (parserKey is "wildberries" or "wb")
            return marketplaceName.Contains("wildberries", StringComparison.Ordinal);

        return false;
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
    }
}
