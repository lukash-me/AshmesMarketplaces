using System.Text.Json;

namespace AshmesMarketplaces.Application.MarketIntelligence.Dtos;

public sealed class PublicTopForecastQuery
{
    public string? SourceCategory { get; init; }
    public string? SourceSubcategory { get; init; }
    public string? Query { get; init; }
    public string? SourceRegionDest { get; init; }
    public string? Sort { get; init; }
    public int TopN { get; init; } = 1000;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 14;
    public decimal MinProbability { get; init; } = 0.7m;
}

public sealed record PublicTopForecastResponse(
    PublicMarketContextDto Context,
    PublicTopForecastRunDto? Run,
    IReadOnlyList<PublicTopForecastItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<string> Limitations);

public sealed record PublicTopForecastRunDto(
    Guid Id,
    string ModelVersion,
    string ModelArtifactId,
    DateTime TrainedAtUtc,
    DateTime CalculatedAtUtc,
    int SampleSize,
    int TrainingSampleSize,
    int ValidationSampleSize,
    int TestSampleSize,
    int PositiveCount,
    int PredictionsCount,
    decimal MinProbability,
    JsonElement? Metrics,
    JsonElement? FeatureSchema,
    IReadOnlyList<string> Warnings);

public sealed record PublicTopForecastItemDto(
    string WbProductId,
    string? WbRootId,
    string? ProductRowId,
    string? Name,
    string? Image,
    decimal? Price,
    decimal? Rating,
    int? FeedbackCount,
    int? Stock,
    int? CurrentPosition,
    string? CurrentPositionState,
    int? ObservedRangeLimit,
    int? PredictedPosition,
    decimal Top100Probability,
    decimal Confidence,
    string? SellerName,
    string? BrandName,
    decimal FeatureCoveragePercent,
    IReadOnlyList<string> Reasons);

public sealed record RecalculateTopForecastResponse(
    Guid RunId,
    string ModelVersion,
    string ModelArtifactId,
    DateTime CalculatedAtUtc,
    int SampleSize,
    int PredictionsCount,
    IReadOnlyList<string> Warnings);
