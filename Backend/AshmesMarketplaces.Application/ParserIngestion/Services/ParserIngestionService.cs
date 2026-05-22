using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AshmesMarketplaces.Application.ParserIngestion.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Entities.Product;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserIngestion.Services;

public sealed class ParserIngestionService : IParserIngestionService
{
    private const string ProductsKind = "products";
    private const string ReviewsKind = "reviews";
    private readonly ApplicationDbContext _dbContext;

    public ParserIngestionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ParserIngestionResult> ValidateProductsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var manifest = LoadManifest(runDirectory, ProductsKind);
        var productsPath = RequiredFile(runDirectory, "products.jsonl");
        return ScanProductsAsync("validate-products", manifest, productsPath, options, cancellationToken);
    }

    public async Task<ParserIngestionResult> ValidateReviewsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        return await ScanReviewRunAsync("validate-reviews", runDirectory, options, cancellationToken);
    }

    private async Task<ParserIngestionResult> ScanReviewRunAsync(
        string mode,
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var manifest = LoadManifest(runDirectory, ReviewsKind);
        var summary = new ImportSummary(mode, manifest.ParserRunId, options.DryRun);

        await ScanReviewRootFetchesAsync(
            RequiredFile(runDirectory, "review_fetch_results.jsonl"),
            manifest,
            options,
            summary,
            cancellationToken);
        await ScanReviewsAsync(
            RequiredFile(runDirectory, "reviews.jsonl"),
            manifest,
            options,
            summary,
            cancellationToken);
        await ScanReviewRepliesAsync(
            RequiredFile(runDirectory, "review_replies.jsonl"),
            manifest,
            options,
            summary,
            cancellationToken);

        return summary.ToResult();
    }

    public async Task<ParserIngestionResult> StageProductsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var manifest = LoadManifest(runDirectory, ProductsKind);
        var productsPath = RequiredFile(runDirectory, "products.jsonl");
        if (options.DryRun)
            return await ScanProductsAsync("stage-products", manifest, productsPath, options, cancellationToken);

        var registered = await RegisterRunAndFilesAsync(
            runDirectory,
            manifest,
            ["manifest.json", "products.jsonl", "products.csv", "products.xlsx", "errors.jsonl", "runner.log"],
            cancellationToken);
        var productsFile = registered.Files["products.jsonl"];
        var execution = await StartExecutionAsync(registered.Run.Id, "stage-products", cancellationToken);
        var summary = new ImportSummary("stage-products", manifest.ParserRunId, isDryRun: false);

        try
        {
            await StageProductRowsAsync(
                productsPath,
                manifest,
                registered.Run,
                productsFile,
                execution,
                options,
                summary,
                cancellationToken);
            await FinishExecutionAsync(execution, summary, "succeeded", cancellationToken);
        }
        catch
        {
            await FinishExecutionAsync(execution, summary, "failed", cancellationToken);
            throw;
        }

        return summary.ToResult();
    }

    public async Task<ParserIngestionResult> StageReviewsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var manifest = LoadManifest(runDirectory, ReviewsKind);
        if (options.DryRun)
            return await ScanReviewRunAsync("stage-reviews", runDirectory, options, cancellationToken);

        var registered = await RegisterRunAndFilesAsync(
            runDirectory,
            manifest,
            [
                "manifest.json",
                "review_fetch_results.jsonl",
                "reviews.jsonl",
                "review_replies.jsonl",
                "errors.jsonl",
                "runner.log"
            ],
            cancellationToken);
        var execution = await StartExecutionAsync(registered.Run.Id, "stage-reviews", cancellationToken);
        var summary = new ImportSummary("stage-reviews", manifest.ParserRunId, isDryRun: false);

        try
        {
            await StageReviewRootFetchesAsync(
                RequiredFile(runDirectory, "review_fetch_results.jsonl"),
                manifest,
                registered.Run,
                registered.Files["review_fetch_results.jsonl"],
                execution,
                options,
                summary,
                cancellationToken);
            var rootFetches = await LoadRootFetchSnapshotsAsync(registered.Run.Id, cancellationToken);
            await StageReviewRowsAsync(
                RequiredFile(runDirectory, "reviews.jsonl"),
                manifest,
                registered.Run,
                registered.Files["reviews.jsonl"],
                execution,
                rootFetches,
                options,
                summary,
                cancellationToken);
            await StageReviewReplyRowsAsync(
                RequiredFile(runDirectory, "review_replies.jsonl"),
                manifest,
                registered.Run,
                registered.Files["review_replies.jsonl"],
                execution,
                rootFetches,
                options,
                summary,
                cancellationToken);
            await FinishExecutionAsync(execution, summary, "succeeded", cancellationToken);
        }
        catch
        {
            await FinishExecutionAsync(execution, summary, "failed", cancellationToken);
            throw;
        }

        return summary.ToResult();
    }

    public async Task<ParserIngestionResult> PromoteProductsAsync(
        string parserRunId,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parserRunId))
            throw new ArgumentException("Parser run id is required.", nameof(parserRunId));

        var run = await _dbContext.ParserRuns
            .FirstOrDefaultAsync(x => x.ParserRunId == parserRunId && x.Kind == ProductsKind, cancellationToken);
        if (run is null)
            throw new InvalidOperationException($"Product parser run '{parserRunId}' is not staged.");

        var summary = new ImportSummary("promote-products", parserRunId, options.DryRun);
        ParserImportExecution? execution = null;
        if (!options.DryRun)
            execution = await StartExecutionAsync(run.Id, "promote-products", cancellationToken);

        try
        {
            var rows = await _dbContext.ParserProductRows
                .AsNoTracking()
                .Where(x => x.IdParserRun == run.Id)
                .OrderBy(x => x.ParsedAtUtc)
                .ThenBy(x => x.SourceLineNumber)
                .ToListAsync(cancellationToken);
            var selectedRows = rows
                .GroupBy(x => new { x.Marketplace, x.WbProductId })
                .Select(x => x.Last())
                .Take(MaxRows(options.MaxRowsPerFile))
                .ToList();
            summary.RowsRead += selectedRows.Count;
            summary.Increment("promotion_candidates", selectedRows.Count);

            foreach (var marketplaceRows in selectedRows.GroupBy(x => x.Marketplace))
            {
                var marketplace = await _dbContext.Marketplaces
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Name.ToLower() == marketplaceRows.Key.ToLower(), cancellationToken);
                if (marketplace is null)
                {
                    summary.RowsSkipped += marketplaceRows.Count();
                    summary.Errors += marketplaceRows.Count();
                    if (execution is not null)
                    {
                        await RecordErrorAsync(
                            execution,
                            run.Id,
                            null,
                            null,
                            "promotion",
                            $"Marketplace '{marketplaceRows.Key}' was not found.",
                            cancellationToken);
                    }

                    continue;
                }

                foreach (var batch in marketplaceRows.Chunk(Math.Clamp(options.BatchSize, 1, 1000)))
                {
                    var externalIds = batch.Select(x => x.WbProductId).Distinct().ToList();
                    var products = await _dbContext.Products
                        .Include(x => x.Images)
                        .Where(x => x.IdMp == marketplace.Id && x.IdOnMp != null && externalIds.Contains(x.IdOnMp))
                        .ToListAsync(cancellationToken);
                    var productsByExternalId = products
                        .GroupBy(x => x.IdOnMp!)
                        .ToDictionary(x => x.Key, x => x.ToList());

                    foreach (var row in batch)
                    {
                        if (!productsByExternalId.TryGetValue(row.WbProductId, out var matches) || matches.Count == 0)
                        {
                            summary.RowsSkipped++;
                            summary.Increment("domain_products_missing");
                            continue;
                        }

                        if (matches.Count > 1)
                        {
                            summary.RowsSkipped++;
                            summary.Errors++;
                            if (execution is not null)
                            {
                                await RecordErrorAsync(
                                    execution,
                                    run.Id,
                                    row.IdParserFile,
                                    row.SourceLineNumber,
                                    "promotion",
                                    $"Multiple domain products match external id '{row.WbProductId}'.",
                                    cancellationToken);
                            }

                            continue;
                        }

                        PromoteExistingProduct(matches[0], row);
                        summary.RowsWritten++;
                    }

                    if (!options.DryRun)
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    else
                        _dbContext.ChangeTracker.Clear();
                }
            }

            if (execution is not null)
                await FinishExecutionAsync(execution, summary, "succeeded", cancellationToken);
        }
        catch
        {
            if (execution is not null)
                await FinishExecutionAsync(execution, summary, "failed", cancellationToken);
            throw;
        }

        return summary.ToResult();
    }

    private async Task<ParserIngestionResult> ScanProductsAsync(
        string mode,
        ManifestInfo manifest,
        string productsPath,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var summary = new ImportSummary(mode, manifest.ParserRunId, options.DryRun);
        await foreach (var source in ReadJsonLinesAsync(productsPath, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                using var row = ParseProductRow(
                    source,
                    Guid.NewGuid(),
                    Guid.NewGuid());
                summary.RowsWritten++;
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
            }
        }

        summary.Increment("product_rows_valid", summary.RowsWritten);
        summary.Increment("product_rows_invalid", summary.Errors);
        return summary.ToResult();
    }

    private async Task StageProductRowsAsync(
        string path,
        ManifestInfo manifest,
        ParserRun run,
        ParserFile file,
        ParserImportExecution execution,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParserProductRow>(options.NormalizedBatchSize);
        var errors = new List<ParserImportError>();
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                rows.Add(ParseProductRow(source, run.Id, file.Id));
            }
            catch (Exception exception)
            {
                summary.RowsSkipped++;
                summary.Errors++;
                errors.Add(CreateError(
                    execution.Id,
                    run.Id,
                    file.Id,
                    source.LineNumber,
                    "product-row",
                    exception.Message));
            }

            if (rows.Count + errors.Count >= options.NormalizedBatchSize)
                await FlushProductBatchAsync(file.Id, rows, errors, summary, cancellationToken);
        }

        await FlushProductBatchAsync(file.Id, rows, errors, summary, cancellationToken);
        summary.Increment("product_rows_staged", summary.RowsWritten);
    }

    private async Task FlushProductBatchAsync(
        Guid fileId,
        List<ParserProductRow> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 && errors.Count == 0)
            return;

        var lineNumbers = rows.Select(x => x.SourceLineNumber).ToList();
        var existing = lineNumbers.Count == 0
            ? []
            : await _dbContext.ParserProductRows
                .AsNoTracking()
                .Where(x => x.IdParserFile == fileId && lineNumbers.Contains(x.SourceLineNumber))
                .Select(x => x.SourceLineNumber)
                .ToListAsync(cancellationToken);
        var existingLines = existing.ToHashSet();
        var newRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserProductRows.AddRange(newRows);
        _dbContext.ParserImportErrors.AddRange(errors);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        rows.Clear();
        errors.Clear();
    }

    private async Task ScanReviewRootFetchesAsync(
        string path,
        ManifestInfo manifest,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                using var row = ParseReviewRootFetch(source, manifest, Guid.NewGuid(), Guid.NewGuid());
                summary.RowsWritten++;
                if (row.IsCappedRootPayload)
                    summary.Increment("capped_root_payloads");
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
            }
        }
    }

    private async Task ScanReviewsAsync(
        string path,
        ManifestInfo manifest,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var flags = new RootFetchSnapshot(null, manifest.IsPartialSnapshot, false, true);
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                using var row = ParseReviewRow(source, Guid.NewGuid(), Guid.NewGuid(), flags);
                summary.RowsWritten++;
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
            }
        }
    }

    private async Task ScanReviewRepliesAsync(
        string path,
        ManifestInfo manifest,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var flags = new RootFetchSnapshot(null, manifest.IsPartialSnapshot, false, true);
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                using var row = ParseReviewReplyRow(source, Guid.NewGuid(), Guid.NewGuid(), flags);
                summary.RowsWritten++;
                if (string.IsNullOrWhiteSpace(row.ReplyIdOnMp))
                    summary.Increment("fallback_reply_identities");
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
            }
        }
    }

    private async Task StageReviewRootFetchesAsync(
        string path,
        ManifestInfo manifest,
        ParserRun run,
        ParserFile file,
        ParserImportExecution execution,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParserReviewRootFetch>(options.NormalizedBatchSize);
        var errors = new List<ParserImportError>();
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                var row = ParseReviewRootFetch(source, manifest, run.Id, file.Id);
                rows.Add(row);
                if (row.IsCappedRootPayload)
                    summary.Increment("capped_root_payloads");
            }
            catch (Exception exception)
            {
                summary.RowsSkipped++;
                summary.Errors++;
                errors.Add(CreateError(execution.Id, run.Id, file.Id, source.LineNumber, "review-root-fetch", exception.Message));
            }

            if (rows.Count + errors.Count >= options.NormalizedBatchSize)
                await FlushReviewRootFetchBatchAsync(file.Id, rows, errors, summary, cancellationToken);
        }

        await FlushReviewRootFetchBatchAsync(file.Id, rows, errors, summary, cancellationToken);
    }

    private async Task FlushReviewRootFetchBatchAsync(
        Guid fileId,
        List<ParserReviewRootFetch> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 && errors.Count == 0)
            return;

        var lineNumbers = rows.Select(x => x.SourceLineNumber).ToList();
        var existing = lineNumbers.Count == 0
            ? []
            : await _dbContext.ParserReviewRootFetches
                .AsNoTracking()
                .Where(x => x.IdParserFile == fileId && lineNumbers.Contains(x.SourceLineNumber))
                .Select(x => x.SourceLineNumber)
                .ToListAsync(cancellationToken);
        var existingLines = existing.ToHashSet();
        var newRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserReviewRootFetches.AddRange(newRows);
        _dbContext.ParserImportErrors.AddRange(errors);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        rows.Clear();
        errors.Clear();
    }

    private async Task StageReviewRowsAsync(
        string path,
        ManifestInfo manifest,
        ParserRun run,
        ParserFile file,
        ParserImportExecution execution,
        IReadOnlyDictionary<string, RootFetchSnapshot> rootFetches,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParserReviewRow>(options.NormalizedBatchSize);
        var errors = new List<ParserImportError>();
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                source.ThrowIfInvalid();
                var sourceRootId = RequiredString(source.Payload.RootElement, "source_wb_root_id");
                var flags = rootFetches.GetValueOrDefault(
                    sourceRootId,
                    new RootFetchSnapshot(null, manifest.IsPartialSnapshot, false, true));
                rows.Add(ParseReviewRow(source, run.Id, file.Id, flags));
            }
            catch (Exception exception)
            {
                summary.RowsSkipped++;
                summary.Errors++;
                errors.Add(CreateError(execution.Id, run.Id, file.Id, source.LineNumber, "review-row", exception.Message));
            }

            if (rows.Count + errors.Count >= options.NormalizedBatchSize)
                await FlushReviewBatchAsync(file.Id, rows, errors, summary, cancellationToken);
        }

        await FlushReviewBatchAsync(file.Id, rows, errors, summary, cancellationToken);
    }

    private async Task FlushReviewBatchAsync(
        Guid fileId,
        List<ParserReviewRow> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 && errors.Count == 0)
            return;

        var lineNumbers = rows.Select(x => x.SourceLineNumber).ToList();
        var existing = lineNumbers.Count == 0
            ? []
            : await _dbContext.ParserReviewRows
                .AsNoTracking()
                .Where(x => x.IdParserFile == fileId && lineNumbers.Contains(x.SourceLineNumber))
                .Select(x => x.SourceLineNumber)
                .ToListAsync(cancellationToken);
        var existingLines = existing.ToHashSet();
        var newRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserReviewRows.AddRange(newRows);
        _dbContext.ParserImportErrors.AddRange(errors);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        rows.Clear();
        errors.Clear();
    }

    private async Task StageReviewReplyRowsAsync(
        string path,
        ManifestInfo manifest,
        ParserRun run,
        ParserFile file,
        ParserImportExecution execution,
        IReadOnlyDictionary<string, RootFetchSnapshot> rootFetches,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParserReviewReplyRow>(options.NormalizedBatchSize);
        var errors = new List<ParserImportError>();
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                source.ThrowIfInvalid();
                var sourceRootId = RequiredString(source.Payload.RootElement, "source_wb_root_id");
                var flags = rootFetches.GetValueOrDefault(
                    sourceRootId,
                    new RootFetchSnapshot(null, manifest.IsPartialSnapshot, false, true));
                var row = ParseReviewReplyRow(source, run.Id, file.Id, flags);
                rows.Add(row);
                if (string.IsNullOrWhiteSpace(row.ReplyIdOnMp))
                    summary.Increment("fallback_reply_identities");
            }
            catch (Exception exception)
            {
                summary.RowsSkipped++;
                summary.Errors++;
                errors.Add(CreateError(execution.Id, run.Id, file.Id, source.LineNumber, "review-reply-row", exception.Message));
            }

            if (rows.Count + errors.Count >= options.NormalizedBatchSize)
                await FlushReviewReplyBatchAsync(file.Id, rows, errors, summary, cancellationToken);
        }

        await FlushReviewReplyBatchAsync(file.Id, rows, errors, summary, cancellationToken);
    }

    private async Task FlushReviewReplyBatchAsync(
        Guid fileId,
        List<ParserReviewReplyRow> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 && errors.Count == 0)
            return;

        var lineNumbers = rows.Select(x => x.SourceLineNumber).ToList();
        var existing = lineNumbers.Count == 0
            ? []
            : await _dbContext.ParserReviewReplyRows
                .AsNoTracking()
                .Where(x => x.IdParserFile == fileId && lineNumbers.Contains(x.SourceLineNumber))
                .Select(x => x.SourceLineNumber)
                .ToListAsync(cancellationToken);
        var existingLines = existing.ToHashSet();
        var newRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserReviewReplyRows.AddRange(newRows);
        _dbContext.ParserImportErrors.AddRange(errors);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        rows.Clear();
        errors.Clear();
    }

    private async Task<IReadOnlyDictionary<string, RootFetchSnapshot>> LoadRootFetchSnapshotsAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.ParserReviewRootFetches
            .AsNoTracking()
            .Where(x => x.IdParserRun == runId)
            .OrderBy(x => x.SourceLineNumber)
            .Select(x => new RootFetchRow(
                x.Id,
                x.SourceWbRootId,
                x.IsPartialSnapshot,
                x.IsCappedRootPayload,
                x.IsFullHistoryUnknown))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.SourceWbRootId)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var latest = x.Last();
                    return new RootFetchSnapshot(
                        latest.Id,
                        latest.IsPartialSnapshot,
                        latest.IsCappedRootPayload,
                        latest.IsFullHistoryUnknown);
                });
    }

    private async Task<RegisteredRun> RegisterRunAndFilesAsync(
        string runDirectory,
        ManifestInfo manifest,
        IReadOnlyCollection<string> fileNames,
        CancellationToken cancellationToken)
    {
        var run = await _dbContext.ParserRuns.FirstOrDefaultAsync(
            x => x.ParserRunId == manifest.ParserRunId
                 && x.Marketplace == manifest.Marketplace
                 && x.Kind == manifest.Kind,
            cancellationToken);
        if (run is null)
        {
            run = new ParserRun(
                manifest.ParserRunId,
                manifest.Marketplace,
                manifest.Kind,
                manifest.Status,
                manifest.SchemaVersion,
                manifest.ParserVersion,
                manifest.StartedAtUtc,
                manifest.FinishedAtUtc,
                CloneDocument(manifest.RequestedScope),
                CloneDocument(manifest.Counters),
                DateTime.UtcNow);
            _dbContext.ParserRuns.Add(run);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var files = new Dictionary<string, ParserFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileName in fileNames)
        {
            var path = Path.Combine(runDirectory, fileName);
            if (!File.Exists(path))
                continue;

            var sha256 = await ComputeFileHashAsync(path, cancellationToken);
            var normalizedPath = Path.GetFullPath(path);
            var parserFile = await _dbContext.ParserFiles.FirstOrDefaultAsync(
                x => x.IdParserRun == run.Id
                     && x.Kind == fileName
                     && x.Path == normalizedPath
                     && x.Sha256 == sha256,
                cancellationToken);
            if (parserFile is null)
            {
                parserFile = new ParserFile(
                    run.Id,
                    fileName,
                    normalizedPath,
                    sha256,
                    new FileInfo(path).Length,
                    rowCount: null,
                    DateTime.UtcNow);
                _dbContext.ParserFiles.Add(parserFile);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            files[fileName] = parserFile;
        }

        return new RegisteredRun(run, files);
    }

    private async Task<ParserImportExecution> StartExecutionAsync(
        Guid runId,
        string mode,
        CancellationToken cancellationToken)
    {
        var execution = new ParserImportExecution(runId, mode, isDryRun: false, DateTime.UtcNow);
        _dbContext.ParserImportExecutions.Add(execution);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return execution;
    }

    private async Task FinishExecutionAsync(
        ParserImportExecution execution,
        ImportSummary summary,
        string status,
        CancellationToken cancellationToken)
    {
        execution.Finish(
            status,
            summary.RowsRead,
            summary.RowsWritten,
            summary.RowsSkipped,
            summary.Errors,
            DateTime.UtcNow);
        _dbContext.ParserImportExecutions.Update(execution);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordErrorAsync(
        ParserImportExecution execution,
        Guid? runId,
        Guid? fileId,
        long? sourceLineNumber,
        string phase,
        string message,
        CancellationToken cancellationToken)
    {
        _dbContext.ParserImportErrors.Add(CreateError(
            execution.Id,
            runId,
            fileId,
            sourceLineNumber,
            phase,
            message));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ParserImportError CreateError(
        Guid executionId,
        Guid? runId,
        Guid? fileId,
        long? sourceLineNumber,
        string phase,
        string message)
    {
        return new ParserImportError(
            executionId,
            runId,
            fileId,
            sourceLineNumber,
            phase,
            "error",
            message,
            details: null,
            DateTime.UtcNow);
    }

    private void PromoteExistingProduct(Product product, ParserProductRow row)
    {
        var entry = _dbContext.Entry(product);
        if (!string.IsNullOrWhiteSpace(row.Name))
            entry.Property(x => x.Name).CurrentValue = row.Name;

        if (!string.IsNullOrWhiteSpace(row.SkuProduct))
            entry.Property(x => x.SkuProduct).CurrentValue = row.SkuProduct;

        foreach (var imageUrl in ImageUrls(row.ImageUrls))
            product.AddImage(imageUrl);
    }

    private static IEnumerable<string> ImageUrls(JsonDocument? imageUrls)
    {
        if (imageUrls?.RootElement.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var item in imageUrls.RootElement.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    private static ManifestInfo LoadManifest(string runDirectory, string kind)
    {
        var path = RequiredFile(runDirectory, "manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        var root = manifest.RootElement;
        var countersName = kind == ProductsKind ? "row_counts" : "counters";
        return new ManifestInfo(
            kind,
            RequiredString(root, "parser_run_id"),
            RequiredString(root, "marketplace"),
            RequiredString(root, "status"),
            ReadInt(root, "schema_version") ?? 1,
            ReadString(root, "parser_version"),
            RequiredUtcDate(root, "started_at_utc"),
            OptionalUtcDate(root, "finished_at_utc"),
            CloneElement(root, "requested_scope"),
            CloneElement(root, countersName));
    }

    private static string RequiredFile(string runDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(runDirectory))
            throw new ArgumentException("Run directory is required.", nameof(runDirectory));

        var path = Path.Combine(runDirectory, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Required parser artifact '{fileName}' was not found.", path);
        return path;
    }

    private static async IAsyncEnumerable<JsonLine> ReadJsonLinesAsync(
        string path,
        long? maxRows,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        long lineNumber = 0;
        long emitted = 0;
        while (!maxRows.HasValue || emitted < maxRows.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonDocument document;
            string? parseError = null;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException exception)
            {
                document = JsonDocument.Parse("{}");
                parseError = $"JSONL line {lineNumber} in '{path}' is not valid JSON: {exception.Message}";
            }

            emitted++;
            yield return new JsonLine(lineNumber, line, document, parseError);
        }
    }

    private static ParserProductRow ParseProductRow(JsonLine source, Guid runId, Guid fileId)
    {
        source.ThrowIfInvalid();
        var row = source.Payload.RootElement;
        return new ParserProductRow(
            runId,
            fileId,
            source.LineNumber,
            RowHash(source.RawLine),
            ReadInt(row, "schema_version") ?? 1,
            RequiredString(row, "marketplace"),
            RequiredString(row, "parser_run_id"),
            RequiredUtcDate(row, "parsed_at_utc"),
            ReadString(row, "source_category"),
            ReadString(row, "source_subcategory"),
            ReadString(row, "source_query"),
            ReadString(row, "source_region_dest"),
            RequiredString(row, "wb_product_id"),
            ReadString(row, "sku_product"),
            RequiredString(row, "name"),
            ReadString(row, "entity"),
            ReadLong(row, "brand_id"),
            ReadString(row, "brand_name"),
            ReadLong(row, "seller_id"),
            ReadString(row, "seller_name"),
            ReadDecimal(row, "price_regular"),
            ReadDecimal(row, "price_discounted"),
            ReadDecimal(row, "price_wb_wallet"),
            ReadInt(row, "discount_percent"),
            ReadInt(row, "total_quantity"),
            ReadInt(row, "rating_rounded"),
            ReadDecimal(row, "review_rating"),
            ReadInt(row, "feedback_count"),
            ReadString(row, "feedback_count_source"),
            CloneDocument(CloneElement(row, "image_urls")),
            ReadInt(row, "image_count"),
            ReadString(row, "wb_root_id"),
            ReadLong(row, "subject_parent_id"),
            ReadLong(row, "subject_id"),
            CloneDocument(CloneElement(row, "raw_observed_fields")));
    }

    private static ParserReviewRootFetch ParseReviewRootFetch(
        JsonLine source,
        ManifestInfo manifest,
        Guid runId,
        Guid fileId)
    {
        source.ThrowIfInvalid();
        var row = source.Payload.RootElement;
        var feedbackCount = ReadInt(row, "payload_feedback_count_if_available");
        var feedbackRowsSeen = ReadInt(row, "payload_feedback_rows_seen") ?? 0;
        return new ParserReviewRootFetch(
            runId,
            fileId,
            source.LineNumber,
            RowHash(source.RawLine),
            RequiredUtcDate(row, "timestamp_utc"),
            RequiredString(row, "parser_run_id"),
            RequiredString(row, "source_wb_root_id"),
            CloneDocument(CloneElement(row, "selected_wb_product_ids")),
            RequiredString(row, "endpoint"),
            ReadInt(row, "attempts") ?? 0,
            ReadInt(row, "retries") ?? 0,
            ReadDecimal(row, "backoff_seconds_total") ?? 0m,
            ReadInt(row, "http_status"),
            RequiredString(row, "status"),
            ReadInt(row, "elapsed_ms") ?? 0,
            feedbackCount,
            feedbackRowsSeen,
            ReadInt(row, "selected_review_rows_seen") ?? 0,
            ReadInt(row, "reviews_written") ?? 0,
            ReadInt(row, "replies_written") ?? 0,
            ReadString(row, "raw_payload_path"),
            ReadString(row, "error_summary"),
            manifest.IsPartialSnapshot,
            feedbackCount.HasValue && feedbackCount.Value > feedbackRowsSeen,
            isFullHistoryUnknown: true);
    }

    private static ParserReviewRow ParseReviewRow(
        JsonLine source,
        Guid runId,
        Guid fileId,
        RootFetchSnapshot flags)
    {
        source.ThrowIfInvalid();
        var row = source.Payload.RootElement;
        var attribution = RequiredRootAttribution(row);
        return new ParserReviewRow(
            runId,
            fileId,
            flags.Id,
            source.LineNumber,
            RowHash(source.RawLine),
            ReadInt(row, "schema_version") ?? 1,
            RequiredString(row, "parser_run_id"),
            RequiredUtcDate(row, "parsed_at_utc"),
            RequiredString(row, "marketplace"),
            ReadString(row, "input_products_parser_run_id"),
            ReadString(row, "input_products_jsonl"),
            RequiredString(row, "source_wb_root_id"),
            RequiredString(row, "wb_product_id"),
            attribution,
            RequiredString(row, "review_id_on_mp"),
            ReadInt(row, "rating"),
            ReadString(row, "text"),
            ReadString(row, "pros"),
            ReadString(row, "cons"),
            OptionalUtcDate(row, "created_at_on_mp"),
            ReadString(row, "reviewer_name"),
            ReadString(row, "reviewer_country"),
            ReadBool(row, "reviewer_has_photo"),
            ReadInt(row, "helpful_plus"),
            ReadInt(row, "helpful_minus"),
            ReadString(row, "source_category"),
            ReadString(row, "source_subcategory"),
            ReadString(row, "source_query"),
            ReadString(row, "source_region_dest"),
            CloneDocument(CloneElement(row, "raw_observed_fields")),
            flags.IsPartialSnapshot,
            flags.IsCappedRootPayload,
            flags.IsFullHistoryUnknown);
    }

    private static ParserReviewReplyRow ParseReviewReplyRow(
        JsonLine source,
        Guid runId,
        Guid fileId,
        RootFetchSnapshot flags)
    {
        source.ThrowIfInvalid();
        var row = source.Payload.RootElement;
        var attribution = RequiredRootAttribution(row);
        return new ParserReviewReplyRow(
            runId,
            fileId,
            flags.Id,
            source.LineNumber,
            RowHash(source.RawLine),
            ReadInt(row, "schema_version") ?? 1,
            RequiredString(row, "parser_run_id"),
            RequiredUtcDate(row, "parsed_at_utc"),
            RequiredString(row, "marketplace"),
            ReadString(row, "input_products_parser_run_id"),
            ReadString(row, "input_products_jsonl"),
            RequiredString(row, "source_wb_root_id"),
            RequiredString(row, "wb_product_id"),
            attribution,
            RequiredString(row, "review_id_on_mp"),
            ReadString(row, "reply_id_on_mp"),
            ReadString(row, "reply_fallback_hash"),
            ReadString(row, "text"),
            OptionalUtcDate(row, "created_at_on_mp"),
            OptionalUtcDate(row, "updated_at_on_mp"),
            ReadString(row, "reply_author"),
            ReadString(row, "reply_state"),
            ReadString(row, "source_category"),
            ReadString(row, "source_subcategory"),
            ReadString(row, "source_query"),
            ReadString(row, "source_region_dest"),
            CloneDocument(CloneElement(row, "raw_observed_fields")),
            flags.IsPartialSnapshot,
            flags.IsCappedRootPayload,
            flags.IsFullHistoryUnknown);
    }

    private static string RequiredRootAttribution(JsonElement row)
    {
        var attribution = RequiredString(row, "review_attribution_mode");
        if (!string.Equals(attribution, "root_payload", StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported review attribution mode '{attribution}'.");
        return attribution;
    }

    private static string RequiredString(JsonElement row, string name)
    {
        var value = ReadString(row, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Required parser field '{name}' is missing.");
        return value;
    }

    private static string? ReadString(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static int? ReadInt(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static long? ReadLong(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            return number;

        return long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static decimal? ReadDecimal(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;

        return decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static bool? ReadBool(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ when bool.TryParse(value.ToString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static DateTime RequiredUtcDate(JsonElement row, string name)
    {
        return OptionalUtcDate(row, name)
               ?? throw new InvalidDataException($"Required UTC parser field '{name}' is missing.");
    }

    private static DateTime? OptionalUtcDate(JsonElement row, string name)
    {
        var value = ReadString(row, name);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new InvalidDataException($"Parser field '{name}' is not a UTC timestamp.");
        }

        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }

    private static JsonElement? CloneElement(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.Clone();
    }

    private static JsonDocument? CloneDocument(JsonElement? element)
    {
        return element.HasValue
            ? JsonDocument.Parse(element.Value.GetRawText())
            : null;
    }

    private static string RowHash(string line)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line))).ToLowerInvariant();
    }

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        using var hash = SHA256.Create();
        var value = await hash.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(value).ToLowerInvariant();
    }

    private static int MaxRows(long? maxRows)
    {
        if (!maxRows.HasValue)
            return int.MaxValue;
        return (int)Math.Clamp(maxRows.Value, 0, int.MaxValue);
    }

    private sealed record ManifestInfo(
        string Kind,
        string ParserRunId,
        string Marketplace,
        string Status,
        int SchemaVersion,
        string? ParserVersion,
        DateTime StartedAtUtc,
        DateTime? FinishedAtUtc,
        JsonElement? RequestedScope,
        JsonElement? Counters)
    {
        public bool IsPartialSnapshot => !string.Equals(Status, "succeeded", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record RegisteredRun(ParserRun Run, IReadOnlyDictionary<string, ParserFile> Files);

    private sealed record RootFetchRow(
        Guid Id,
        string SourceWbRootId,
        bool IsPartialSnapshot,
        bool IsCappedRootPayload,
        bool IsFullHistoryUnknown);

    private sealed record RootFetchSnapshot(
        Guid? Id,
        bool IsPartialSnapshot,
        bool IsCappedRootPayload,
        bool IsFullHistoryUnknown);

    private sealed class JsonLine : IDisposable
    {
        public JsonLine(long lineNumber, string rawLine, JsonDocument payload, string? parseError)
        {
            LineNumber = lineNumber;
            RawLine = rawLine;
            Payload = payload;
            ParseError = parseError;
        }

        public long LineNumber { get; }
        public string RawLine { get; }
        public JsonDocument Payload { get; }
        public string? ParseError { get; }

        public void ThrowIfInvalid()
        {
            if (!string.IsNullOrWhiteSpace(ParseError))
                throw new InvalidDataException(ParseError);
        }

        public void Dispose()
        {
            Payload.Dispose();
        }
    }

    private sealed class ImportSummary
    {
        private readonly Dictionary<string, long> _details = new(StringComparer.Ordinal);

        public ImportSummary(string mode, string parserRunId, bool isDryRun)
        {
            Mode = mode;
            ParserRunId = parserRunId;
            IsDryRun = isDryRun;
        }

        public string Mode { get; }
        public string ParserRunId { get; }
        public bool IsDryRun { get; }
        public long RowsRead { get; set; }
        public long RowsWritten { get; set; }
        public long RowsSkipped { get; set; }
        public long Errors { get; set; }

        public void Increment(string name, long count = 1)
        {
            _details[name] = _details.GetValueOrDefault(name) + count;
        }

        public ParserIngestionResult ToResult()
        {
            return new ParserIngestionResult(
                Mode,
                ParserRunId,
                IsDryRun,
                RowsRead,
                RowsWritten,
                RowsSkipped,
                Errors,
                _details);
        }
    }
}
