using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Marketplaces;

public sealed class PublicTopForecastRun
{
    public const string CompletedStatus = "completed";
    public const string FailedStatus = "failed";

    private readonly List<PublicTopForecastPrediction> _predictions = [];

    private PublicTopForecastRun() { }

    public PublicTopForecastRun(
        string modelVersion,
        string modelArtifactId,
        DateTime trainedAtUtc,
        DateTime calculatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(modelVersion))
            throw new ArgumentException("Model version is required.", nameof(modelVersion));
        if (string.IsNullOrWhiteSpace(modelArtifactId))
            throw new ArgumentException("Model artifact id is required.", nameof(modelArtifactId));

        DateTimeUtc.EnsureUtc(trainedAtUtc, nameof(trainedAtUtc));
        DateTimeUtc.EnsureUtc(calculatedAtUtc, nameof(calculatedAtUtc));

        Id = Guid.NewGuid();
        ModelVersion = modelVersion.Trim();
        ModelArtifactId = modelArtifactId.Trim();
        TrainedAtUtc = trainedAtUtc;
        CalculatedAtUtc = calculatedAtUtc;
        Status = CompletedStatus;
        MetricsJson = "{}";
        FeatureSchemaJson = "{}";
        WarningsJson = "[]";
        CreatedAtUtc = calculatedAtUtc;
        UpdatedAtUtc = calculatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ModelVersion { get; private set; } = string.Empty;
    public string ModelArtifactId { get; private set; } = string.Empty;
    public DateTime TrainedAtUtc { get; private set; }
    public DateTime CalculatedAtUtc { get; private set; }
    public int SampleSize { get; private set; }
    public int TrainingSampleSize { get; private set; }
    public int ValidationSampleSize { get; private set; }
    public int TestSampleSize { get; private set; }
    public int PositiveCount { get; private set; }
    public int PredictionsCount { get; private set; }
    public decimal MinProbability { get; private set; }
    public string MetricsJson { get; private set; } = "{}";
    public string FeatureSchemaJson { get; private set; } = "{}";
    public string WarningsJson { get; private set; } = "[]";
    public string Status { get; private set; } = CompletedStatus;
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<PublicTopForecastPrediction> Predictions => _predictions;

    public void SetSummary(
        int sampleSize,
        int trainingSampleSize,
        int validationSampleSize,
        int testSampleSize,
        int positiveCount,
        int predictionsCount,
        decimal minProbability,
        string metricsJson,
        string featureSchemaJson,
        string warningsJson)
    {
        SampleSize = sampleSize;
        TrainingSampleSize = trainingSampleSize;
        ValidationSampleSize = validationSampleSize;
        TestSampleSize = testSampleSize;
        PositiveCount = positiveCount;
        PredictionsCount = predictionsCount;
        MinProbability = minProbability;
        MetricsJson = string.IsNullOrWhiteSpace(metricsJson) ? "{}" : metricsJson;
        FeatureSchemaJson = string.IsNullOrWhiteSpace(featureSchemaJson) ? "{}" : featureSchemaJson;
        WarningsJson = string.IsNullOrWhiteSpace(warningsJson) ? "[]" : warningsJson;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string error, DateTime failedAtUtc)
    {
        DateTimeUtc.EnsureUtc(failedAtUtc, nameof(failedAtUtc));

        Status = FailedStatus;
        Error = string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Trim();
        UpdatedAtUtc = failedAtUtc;
    }
}
