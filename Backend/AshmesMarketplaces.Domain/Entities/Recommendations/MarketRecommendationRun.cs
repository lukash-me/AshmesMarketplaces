using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Recommendations;

public sealed class MarketRecommendationRun : IDisposable
{
    public const string HotProductsKind = "hot_products";

    private MarketRecommendationRun() { }

    public MarketRecommendationRun(
        string kind,
        string marketplace,
        string? sourceCategory,
        JsonDocument sourceSubcategories,
        string? productParserRunId,
        string? rankParserRunId,
        JsonDocument reviewParserRunIds,
        string intelligenceRequestId,
        string algorithm,
        string algorithmVersion,
        string modelVersion,
        string inputSnapshotHash,
        string status,
        DateTime requestedAtUtc,
        DateTime? completedAtUtc,
        DateTime? validUntilUtc,
        int productCountSent,
        int recommendationsCount,
        int warningCount,
        string? errorCode,
        string? errorMessage,
        JsonDocument? rawWarnings,
        JsonDocument configOptions,
        DateTime createdAtUtc)
    {
        if (!string.Equals(kind, HotProductsKind, StringComparison.Ordinal))
            throw new ArgumentException("Market recommendation run kind must be hot_products.", nameof(kind));

        if (string.IsNullOrWhiteSpace(marketplace))
            throw new ArgumentException("Marketplace is required.", nameof(marketplace));

        if (string.IsNullOrWhiteSpace(intelligenceRequestId))
            throw new ArgumentException("Intelligence request id is required.", nameof(intelligenceRequestId));

        if (string.IsNullOrWhiteSpace(algorithm))
            throw new ArgumentException("Algorithm is required.", nameof(algorithm));

        if (string.IsNullOrWhiteSpace(algorithmVersion))
            throw new ArgumentException("Algorithm version is required.", nameof(algorithmVersion));

        if (string.IsNullOrWhiteSpace(modelVersion))
            throw new ArgumentException("Model version is required.", nameof(modelVersion));

        if (string.IsNullOrWhiteSpace(inputSnapshotHash))
            throw new ArgumentException("Input snapshot hash is required.", nameof(inputSnapshotHash));

        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status is required.", nameof(status));

        if (productCountSent < 0)
            throw new ArgumentOutOfRangeException(nameof(productCountSent), "Product count must be non-negative.");

        if (recommendationsCount < 0)
            throw new ArgumentOutOfRangeException(nameof(recommendationsCount), "Recommendations count must be non-negative.");

        if (warningCount < 0)
            throw new ArgumentOutOfRangeException(nameof(warningCount), "Warning count must be non-negative.");

        DateTimeUtc.EnsureUtc(requestedAtUtc, nameof(requestedAtUtc));
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        DateTimeUtc.EnsureUtc(validUntilUtc, nameof(validUntilUtc));
        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        Kind = kind;
        Marketplace = marketplace;
        SourceCategory = sourceCategory;
        SourceSubcategories = sourceSubcategories ?? throw new ArgumentNullException(nameof(sourceSubcategories));
        ProductParserRunId = productParserRunId;
        RankParserRunId = rankParserRunId;
        ReviewParserRunIds = reviewParserRunIds ?? throw new ArgumentNullException(nameof(reviewParserRunIds));
        IntelligenceRequestId = intelligenceRequestId;
        Algorithm = algorithm;
        AlgorithmVersion = algorithmVersion;
        ModelVersion = modelVersion;
        InputSnapshotHash = inputSnapshotHash;
        Status = status;
        RequestedAtUtc = requestedAtUtc;
        CompletedAtUtc = completedAtUtc;
        ValidUntilUtc = validUntilUtc;
        ProductCountSent = productCountSent;
        RecommendationsCount = recommendationsCount;
        WarningCount = warningCount;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        RawWarnings = rawWarnings;
        ConfigOptions = configOptions ?? throw new ArgumentNullException(nameof(configOptions));
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Marketplace { get; private set; } = string.Empty;
    public string? SourceCategory { get; private set; }
    public JsonDocument SourceSubcategories { get; private set; } = null!;
    public string? ProductParserRunId { get; private set; }
    public string? RankParserRunId { get; private set; }
    public JsonDocument ReviewParserRunIds { get; private set; } = null!;
    public string IntelligenceRequestId { get; private set; } = string.Empty;
    public string Algorithm { get; private set; } = string.Empty;
    public string AlgorithmVersion { get; private set; } = string.Empty;
    public string ModelVersion { get; private set; } = string.Empty;
    public string InputSnapshotHash { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? ValidUntilUtc { get; private set; }
    public int ProductCountSent { get; private set; }
    public int RecommendationsCount { get; private set; }
    public int WarningCount { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public JsonDocument? RawWarnings { get; private set; }
    public JsonDocument ConfigOptions { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    public void Dispose()
    {
        SourceSubcategories?.Dispose();
        ReviewParserRunIds?.Dispose();
        RawWarnings?.Dispose();
        ConfigOptions?.Dispose();
    }
}
