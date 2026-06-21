using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public static class PublicMarketIntelligenceContextCatalog
{
    public const string SourceCategory = "Товары для дома";
    public const string SourceRegionDest = "12354108";
    public const string Sort = "popular";
    public const int TopN = 1000;

    private static readonly PublicMarketIntelligenceQuery[] Contexts =
    [
        Build("Коврики для ванной"),
        Build("Органайзеры для хранения вещей"),
        Build("Светильники бра")
    ];

    public static IReadOnlyList<PublicMarketIntelligenceQuery> All => Contexts;

    public static PublicMarketIntelligenceQuery? Resolve(PublicMarketIntelligenceQuery query)
    {
        var sourceSubcategory = Normalize(query.SourceSubcategory)
            ?? Normalize(query.Query)
            ?? Contexts[0].SourceSubcategory;

        return Contexts.FirstOrDefault(x => string.Equals(x.SourceSubcategory, sourceSubcategory, StringComparison.Ordinal));
    }

    private static PublicMarketIntelligenceQuery Build(string sourceSubcategory) =>
        new()
        {
            SourceCategory = SourceCategory,
            SourceSubcategory = sourceSubcategory,
            Query = sourceSubcategory,
            SourceRegionDest = SourceRegionDest,
            Sort = Sort,
            TopN = TopN
        };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
