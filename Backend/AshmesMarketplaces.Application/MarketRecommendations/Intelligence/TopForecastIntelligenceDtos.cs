namespace AshmesMarketplaces.Application.MarketRecommendations.Intelligence;

public sealed record TopForecastTrainRequest(
    string RequestId,
    DateTime GeneratedAtUtc,
    string Marketplace,
    HotProductsIntelligenceScope Scope,
    IReadOnlyList<MarketProductFeatureDto> Products,
    TopForecastOptions Options);

public sealed record TopForecastPredictRequest(
    string RequestId,
    DateTime GeneratedAtUtc,
    string Marketplace,
    string ModelArtifactId,
    IReadOnlyList<MarketProductFeatureDto> Products,
    TopForecastOptions Options);

public sealed record TopForecastOptions(
    string Algorithm,
    int TopThreshold,
    decimal MinProbability,
    int RandomSeed,
    decimal TrainFraction,
    decimal ValidationFraction,
    decimal TestFraction,
    int Iterations);

public sealed record TopForecastTrainResponse(
    string RequestId,
    string Status,
    string Algorithm,
    string AlgorithmVersion,
    string ModelVersion,
    string? ModelArtifactId,
    DateTime TrainedAtUtc,
    IReadOnlyList<string> FeatureNames,
    IReadOnlyList<string> CategoricalFeatureNames,
    IReadOnlyList<TopForecastMetricsDto> Metrics,
    int SampleSize,
    int TrainingSampleSize,
    int ValidationSampleSize,
    int TestSampleSize,
    int PositiveCount,
    IReadOnlyList<string> Warnings);

public sealed record TopForecastPredictResponse(
    string RequestId,
    string Status,
    string Algorithm,
    string AlgorithmVersion,
    string ModelVersion,
    string ModelArtifactId,
    DateTime ComputedAtUtc,
    IReadOnlyList<TopForecastPredictionDto> Predictions,
    IReadOnlyList<string> Warnings);

public sealed record TopForecastMetricsDto(
    string Split,
    int SampleSize,
    int PositiveCount,
    decimal? Accuracy,
    decimal? Precision,
    decimal? Recall,
    decimal? F1,
    decimal? RocAuc,
    decimal? PositionMae);

public sealed record TopForecastPredictionDto(
    string? ProductKey,
    string? WbProductId,
    string? WbRootId,
    int? PredictedPosition,
    decimal Top100Probability,
    decimal Confidence,
    decimal FeatureCoveragePercent,
    IReadOnlyList<string> Reasons);
