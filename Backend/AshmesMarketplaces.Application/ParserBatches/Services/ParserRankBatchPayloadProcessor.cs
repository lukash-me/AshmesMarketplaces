using System.Text.Json;
using AshmesMarketplaces.Application.ParserIngestion.Dtos;
using AshmesMarketplaces.Application.ParserIngestion.Services;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserRankBatchPayloadProcessor
{
    private const int SupportedSchemaVersion = 1;
    private const string RankSnapshotBatchKind = "rank_snapshot_batch";

    private readonly IParserIngestionService _ingestionService;
    private readonly string _restoreRoot;

    public ParserRankBatchPayloadProcessor(IParserIngestionService ingestionService)
        : this(ingestionService, Path.Combine(Path.GetTempPath(), "ashmes-parser-rank-batches"))
    {
    }

    internal ParserRankBatchPayloadProcessor(IParserIngestionService ingestionService, string restoreRoot)
    {
        _ingestionService = ingestionService;
        _restoreRoot = restoreRoot;
    }

    public async Task<ParserBatchPayloadProcessingSummary> ProcessAsync(
        ParserBatchSubmission batch,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        ValidateEnvelope(batch, payload);

        var restoreDirectory = PrepareRestoreDirectory(batch.Id);
        try
        {
            RestoreArtifacts(payload, restoreDirectory);

            var result = await _ingestionService.StageRanksAsync(
                restoreDirectory,
                new ParserIngestionOptions(
                    DryRun: false,
                    CdcContext: batch.ParserProxyRunId.HasValue
                        ? new ParserCdcApplyContext(batch.ParserProxyRunId.Value, batch.Id, batch.ParserCycleId)
                        : null),
                cancellationToken);

            return new ParserBatchPayloadProcessingSummary(
                result.Mode,
                result.RowsRead,
                result.RowsWritten,
                result.RowsSkipped,
                result.ErrorCount);
        }
        finally
        {
            TryDeleteDirectory(restoreDirectory);
        }
    }

    private static void ValidateEnvelope(ParserBatchSubmission batch, JsonElement payload)
    {
        if (!string.Equals(batch.BatchKind, RankSnapshotBatchKind, StringComparison.Ordinal))
            throw new ParserBatchFinalException($"Unsupported parser batch kind '{batch.BatchKind}'.");

        if (payload.ValueKind != JsonValueKind.Object)
            throw new ParserBatchFinalException("Batch payload root must be a JSON object.");

        var schemaVersion = ReadInt(payload, "schemaVersion", "schema_version");
        if (schemaVersion != SupportedSchemaVersion)
            throw new ParserBatchFinalException($"Unsupported parser batch schema version '{schemaVersion?.ToString() ?? "null"}'.");

        if (!payload.TryGetProperty("artifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Object)
            throw new ParserBatchFinalException("Rank batch payload must contain an artifacts object.");

        if (!artifacts.EnumerateObject().Any())
            throw new ParserBatchFinalException("Rank batch payload artifacts object is empty.");
    }

    private string PrepareRestoreDirectory(Guid batchId)
    {
        Directory.CreateDirectory(_restoreRoot);
        var restoreDirectory = Path.Combine(_restoreRoot, batchId.ToString("N"));
        if (Directory.Exists(restoreDirectory))
            Directory.Delete(restoreDirectory, recursive: true);

        Directory.CreateDirectory(restoreDirectory);
        return restoreDirectory;
    }

    private static void RestoreArtifacts(JsonElement payload, string restoreDirectory)
    {
        var artifactPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var artifacts = payload.GetProperty("artifacts");

        foreach (var artifact in artifacts.EnumerateObject())
        {
            var relativePath = NormalizeRelativeArtifactPath(artifact.Name);
            if (artifact.Value.ValueKind != JsonValueKind.String)
                throw new ParserBatchFinalException($"Artifact '{artifact.Name}' value must be a JSON string.");

            var targetPath = Path.GetFullPath(Path.Combine(restoreDirectory, relativePath));
            var restoreRoot = Path.GetFullPath(restoreDirectory);
            if (!targetPath.StartsWith(restoreRoot, StringComparison.OrdinalIgnoreCase))
                throw new ParserBatchFinalException($"Artifact '{artifact.Name}' points outside restored rank directory.");

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(targetPath, artifact.Value.GetString() ?? string.Empty);
            artifactPaths.Add(relativePath);
        }

        foreach (var required in new[] { "manifest.json", "product_rank_snapshots.jsonl", "rank_page_fetches.jsonl", "errors.jsonl" })
        {
            if (!artifactPaths.Contains(required))
                throw new ParserBatchFinalException($"Required rank artifact '{required}' was not found.");
        }
    }

    private static string NormalizeRelativeArtifactPath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ParserBatchFinalException("Artifact path is empty.");
        if (Path.IsPathFullyQualified(normalized))
            throw new ParserBatchFinalException($"Artifact path '{path}' must be relative.");

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(x => x == "." || x == ".."))
            throw new ParserBatchFinalException($"Artifact path '{path}' contains unsafe segments.");

        return string.Join("/", segments);
    }

    private static int? ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
                return number;
        }

        return null;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Temp cleanup must not hide processing status.
        }
    }
}
