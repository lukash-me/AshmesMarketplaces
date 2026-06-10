using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Workspaces;

public sealed class WorkspaceMarketProductAnalysisRun : IDisposable
{
    public const string CompletedStatus = "completed";
    public const string FailedStatus = "failed";

    private WorkspaceMarketProductAnalysisRun() { }

    public WorkspaceMarketProductAnalysisRun(
        Guid idWorkspace,
        string status,
        DateTime startedAtUtc,
        DateTime? completedAtUtc,
        string algorithm,
        string algorithmVersion,
        string modelVersion,
        int productCount,
        int signalCount,
        int similarProductCount,
        JsonDocument warnings,
        string? errorMessage)
    {
        if (idWorkspace == Guid.Empty)
            throw new ArgumentException("Workspace id is required.", nameof(idWorkspace));

        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status is required.", nameof(status));

        if (string.IsNullOrWhiteSpace(algorithm))
            throw new ArgumentException("Algorithm is required.", nameof(algorithm));

        if (string.IsNullOrWhiteSpace(algorithmVersion))
            throw new ArgumentException("Algorithm version is required.", nameof(algorithmVersion));

        if (string.IsNullOrWhiteSpace(modelVersion))
            throw new ArgumentException("Model version is required.", nameof(modelVersion));

        if (productCount < 0 || signalCount < 0 || similarProductCount < 0)
            throw new ArgumentOutOfRangeException(nameof(productCount), "Counts must be non-negative.");

        DateTimeUtc.EnsureUtc(startedAtUtc, nameof(startedAtUtc));
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));

        Id = Guid.NewGuid();
        IdWorkspace = idWorkspace;
        Status = status.Trim();
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Algorithm = algorithm.Trim();
        AlgorithmVersion = algorithmVersion.Trim();
        ModelVersion = modelVersion.Trim();
        ProductCount = productCount;
        SignalCount = signalCount;
        SimilarProductCount = similarProductCount;
        Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
    }

    public Guid Id { get; private set; }
    public Guid IdWorkspace { get; private set; }
    public string Status { get; private set; } = CompletedStatus;
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string Algorithm { get; private set; } = string.Empty;
    public string AlgorithmVersion { get; private set; } = string.Empty;
    public string ModelVersion { get; private set; } = string.Empty;
    public int ProductCount { get; private set; }
    public int SignalCount { get; private set; }
    public int SimilarProductCount { get; private set; }
    public JsonDocument Warnings { get; private set; } = null!;
    public string? ErrorMessage { get; private set; }

    public void Dispose()
    {
        Warnings?.Dispose();
    }
}
