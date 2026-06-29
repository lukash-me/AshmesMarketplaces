using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserBatchArtifact : IDisposable
{
    private ParserBatchArtifact() { }

    public ParserBatchArtifact(Guid batchSubmissionId, string artifactKind, JsonDocument payloadJson, DateTime createdAtUtc)
    {
        if (batchSubmissionId == Guid.Empty)
            throw new ArgumentException("Batch submission id is required.", nameof(batchSubmissionId));
        if (string.IsNullOrWhiteSpace(artifactKind))
            throw new ArgumentException("Artifact kind is required.", nameof(artifactKind));

        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        BatchSubmissionId = batchSubmissionId;
        ArtifactKind = artifactKind.Trim();
        PayloadJson = payloadJson;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid BatchSubmissionId { get; private set; }
    public string ArtifactKind { get; private set; } = string.Empty;
    public JsonDocument PayloadJson { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    public void Dispose()
    {
        PayloadJson?.Dispose();
    }
}
