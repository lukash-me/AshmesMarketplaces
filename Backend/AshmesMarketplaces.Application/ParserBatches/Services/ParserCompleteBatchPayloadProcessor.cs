using System.Text.Json;
using System.Text.Json.Nodes;
using AshmesMarketplaces.Application.ParserIngestion.Dtos;
using AshmesMarketplaces.Application.ParserIngestion.Services;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserCompleteBatchPayloadProcessor : IParserBatchPayloadProcessor
{
    private const int SupportedSchemaVersion = 1;
    private const string CompleteCardBatchKind = "complete_card_batch";

    private readonly IParserIngestionService _ingestionService;
    private readonly string _restoreRoot;

    public ParserCompleteBatchPayloadProcessor(IParserIngestionService ingestionService)
        : this(ingestionService, Path.Combine(Path.GetTempPath(), "ashmes-parser-batches"))
    {
    }

    internal ParserCompleteBatchPayloadProcessor(IParserIngestionService ingestionService, string restoreRoot)
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
            var artifactPaths = RestoreArtifacts(payload, restoreDirectory);
            RewriteBatchManifest(payload, restoreDirectory, artifactPaths);

            var result = await _ingestionService.StageCompleteBatchAsync(
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
        if (!string.Equals(batch.BatchKind, CompleteCardBatchKind, StringComparison.Ordinal))
            throw new ParserBatchFinalException($"Unsupported parser batch kind '{batch.BatchKind}'.");

        if (payload.ValueKind != JsonValueKind.Object)
            throw new ParserBatchFinalException("Batch payload root must be a JSON object.");

        var schemaVersion = ReadInt(payload, "schemaVersion", "schema_version");
        if (schemaVersion != SupportedSchemaVersion)
            throw new ParserBatchFinalException($"Unsupported parser batch schema version '{schemaVersion?.ToString() ?? "null"}'.");

        if (!payload.TryGetProperty("artifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Object)
            throw new ParserBatchFinalException("Batch payload must contain an artifacts object.");

        if (!artifacts.EnumerateObject().Any())
            throw new ParserBatchFinalException("Batch payload artifacts object is empty.");
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

    private static IReadOnlySet<string> RestoreArtifacts(JsonElement payload, string restoreDirectory)
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
                throw new ParserBatchFinalException($"Artifact '{artifact.Name}' points outside restored batch directory.");

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(targetPath, artifact.Value.GetString() ?? string.Empty);
            artifactPaths.Add(relativePath);
        }

        if (!artifactPaths.Contains("batch_manifest.json"))
            throw new ParserBatchFinalException("Required artifact 'batch_manifest.json' was not found.");

        return artifactPaths;
    }

    private static void RewriteBatchManifest(
        JsonElement payload,
        string restoreDirectory,
        IReadOnlySet<string> artifactPaths)
    {
        var manifestPath = Path.Combine(restoreDirectory, "batch_manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))
            ?? throw new ParserBatchFinalException("Batch manifest is empty.");
        var root = manifest.AsObject();
        var artifactDirectories = ArtifactDirectories(artifactPaths);
        var originalBatchDir = ReadString(payload, "batchDir", "batch_dir");

        var productRunDir = ReadString(root, "product_run_dir")
            ?? throw new ParserBatchFinalException("Batch manifest does not contain product_run_dir.");
        root["product_run_dir"] = ResolveLocalRunDirectory(
            productRunDir,
            originalBatchDir,
            artifactDirectories,
            "products");

        if (root["steps"] is not JsonArray steps)
            throw new ParserBatchFinalException("Batch manifest does not contain steps array.");

        foreach (var stepNode in steps)
        {
            if (stepNode is not JsonObject step)
                continue;

            var stepName = ReadString(step, "step");
            if (string.IsNullOrWhiteSpace(stepName))
                continue;

            if (step["output_run_dirs"] is not JsonArray outputRunDirs)
                continue;

            var rewritten = new JsonArray();
            foreach (var item in outputRunDirs)
            {
                if (item is null)
                    continue;
                var value = item.GetValue<string>();
                rewritten.Add(ResolveLocalRunDirectory(
                    value,
                    originalBatchDir,
                    artifactDirectories,
                    stepName));
            }

            step["output_run_dirs"] = rewritten;
        }

        File.WriteAllText(
            manifestPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ResolveLocalRunDirectory(
        string originalPath,
        string? originalBatchDir,
        IReadOnlySet<string> artifactDirectories,
        string fallbackPrefix)
    {
        var normalized = TryRelativeToOriginalBatch(originalPath, originalBatchDir);
        if (normalized is not null && artifactDirectories.Contains(normalized))
            return normalized;

        var fallback = FindSingleManifestDirectory(artifactDirectories, fallbackPrefix);
        if (fallback is not null)
            return fallback;

        var leaf = Path.GetFileName(originalPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.IsNullOrWhiteSpace(leaf))
        {
            var matching = artifactDirectories
                .Where(x => string.Equals(Path.GetFileName(x), leaf, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matching.Count == 1)
                return matching[0];
        }

        throw new ParserBatchFinalException($"Cannot map parser artifact path '{originalPath}' to restored batch artifacts.");
    }

    private static string? TryRelativeToOriginalBatch(string originalPath, string? originalBatchDir)
    {
        if (string.IsNullOrWhiteSpace(originalPath) || string.IsNullOrWhiteSpace(originalBatchDir))
            return null;

        try
        {
            var fullOriginal = Path.GetFullPath(originalPath);
            var fullBatch = Path.GetFullPath(originalBatchDir);
            if (!fullOriginal.StartsWith(fullBatch, StringComparison.OrdinalIgnoreCase))
                return null;

            return NormalizeRelativeArtifactPath(Path.GetRelativePath(fullBatch, fullOriginal));
        }
        catch (Exception) when (originalPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return null;
        }
    }

    private static string? FindSingleManifestDirectory(IReadOnlySet<string> artifactDirectories, string prefix)
    {
        var normalizedPrefix = NormalizePathSeparators(prefix).Trim('/');
        var matches = artifactDirectories
            .Where(x =>
                string.Equals(x, normalizedPrefix, StringComparison.OrdinalIgnoreCase) ||
                x.StartsWith(normalizedPrefix + "/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static HashSet<string> ArtifactDirectories(IReadOnlySet<string> artifactPaths)
    {
        return artifactPaths
            .Where(x => x.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase))
            .Select(x => x[..^"/manifest.json".Length])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativeArtifactPath(string path)
    {
        var normalized = NormalizePathSeparators(path).Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ParserBatchFinalException("Artifact path is empty.");
        if (Path.IsPathFullyQualified(normalized))
            throw new ParserBatchFinalException($"Artifact path '{path}' must be relative.");

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(x => x == "." || x == ".."))
            throw new ParserBatchFinalException($"Artifact path '{path}' contains unsafe segments.");

        return string.Join("/", segments);
    }

    private static string NormalizePathSeparators(string path) =>
        path.Replace('\\', '/');

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

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private static string? ReadString(JsonObject element, string name)
    {
        return element.TryGetPropertyValue(name, out var value) && value is JsonValue jsonValue
            ? jsonValue.GetValue<string>()
            : null;
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
