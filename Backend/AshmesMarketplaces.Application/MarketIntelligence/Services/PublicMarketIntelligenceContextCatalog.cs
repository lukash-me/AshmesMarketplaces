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

        var supportedContext = Contexts.FirstOrDefault(x => string.Equals(x.SourceSubcategory, sourceSubcategory, StringComparison.Ordinal));
        if (supportedContext is null)
            return null;

        var sourceCategory = Normalize(query.SourceCategory);
        var rankQuery = Normalize(query.Query);
        var sourceRegionDest = Normalize(query.SourceRegionDest);
        var sort = Normalize(query.Sort);
        if (sourceCategory is not null
            && rankQuery is not null
            && sourceRegionDest is not null
            && sort is not null)
        {
            return Build(sourceCategory, sourceSubcategory ?? supportedContext.SourceSubcategory!, rankQuery, sourceRegionDest, sort, query.TopN);
        }

        return supportedContext;
    }

    private static PublicMarketIntelligenceQuery Build(
        string sourceCategory,
        string sourceSubcategory,
        string query,
        string sourceRegionDest = SourceRegionDest,
        string sort = Sort,
        int topN = TopN) =>
        new()
        {
            SourceCategory = sourceCategory,
            SourceSubcategory = sourceSubcategory,
            Query = query,
            SourceRegionDest = sourceRegionDest,
            Sort = sort,
            TopN = topN <= 0 ? TopN : topN
        };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
