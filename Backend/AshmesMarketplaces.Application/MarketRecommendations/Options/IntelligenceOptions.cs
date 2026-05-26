namespace AshmesMarketplaces.Application.MarketRecommendations.Options;

public sealed class IntelligenceOptions
{
    public const string SectionName = "Intelligence";
    public const string DefaultBaseUrl = "http://127.0.0.1:8020";
    public const string DefaultHotProductsAlgorithm = "rule_based_hot_products_v1";
    public const int DefaultTimeoutSeconds = 20;
    public const int DefaultMaxProductsPerRequest = 1000;

    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = DefaultBaseUrl;
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;
    public int MaxProductsPerRequest { get; init; } = DefaultMaxProductsPerRequest;
    public string HotProductsAlgorithm { get; init; } = DefaultHotProductsAlgorithm;
}
