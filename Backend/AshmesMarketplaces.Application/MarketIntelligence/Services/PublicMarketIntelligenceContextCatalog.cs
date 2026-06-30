using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public static class PublicMarketIntelligenceContextCatalog
{
    public const string SourceRegionDest = "12354108";
    public const string Sort = "popular";
    public const int TopN = 1000;

    private static readonly PublicMarketIntelligenceQuery[] Contexts =
    [
        Build("Женщинам", "Платья и сарафаны", "menu_v3_8137 платье женские"),
        Build("Обувь", "Кеды и кроссовки", "menu_redirect_subject_v2_8194 мужские кеды и кроссовки"),
        Build("Красота", "Органическая косметика", "menu_redirect_subject_v2_10012 органическая косметика")
    ];

    public static IReadOnlyList<PublicMarketIntelligenceQuery> All => Contexts;

    public static PublicMarketIntelligenceQuery? Resolve(PublicMarketIntelligenceQuery query)
    {
        var sourceSubcategory = Normalize(query.SourceSubcategory)
            ?? Normalize(query.Query)
            ?? Contexts[0].SourceSubcategory;

        return Contexts.FirstOrDefault(x => string.Equals(x.SourceSubcategory, sourceSubcategory, StringComparison.Ordinal));
    }

    private static PublicMarketIntelligenceQuery Build(string sourceCategory, string sourceSubcategory, string query) =>
        new()
        {
            SourceCategory = sourceCategory,
            SourceSubcategory = sourceSubcategory,
            Query = query,
            SourceRegionDest = SourceRegionDest,
            Sort = Sort,
            TopN = TopN
        };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
