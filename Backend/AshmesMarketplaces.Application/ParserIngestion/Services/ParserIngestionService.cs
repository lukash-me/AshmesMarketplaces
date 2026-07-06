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

public sealed partial class ParserIngestionService : IParserIngestionService
{
    private const string ProductsKind = "products";
    private const string ReviewsKind = "reviews";
    private const string RanksKind = "ranks";
    private const string LogisticsKind = "logistics";
    private const string LogisticsRunKind = "wb_logistics";
    private const string ProductDetailsKind = "product_details";
    private readonly ApplicationDbContext _dbContext;
    private readonly ParserProductCdcService _cdcService;

    public ParserIngestionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        _cdcService = new ParserProductCdcService(dbContext);
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

    public async Task<ParserIngestionResult> ValidateRanksAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        return await ScanRankRunAsync("validate-ranks", runDirectory, options, cancellationToken);
    }

    public async Task<ParserIngestionResult> ValidateLogisticsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        return await ScanLogisticsRunAsync("validate-logistics", runDirectory, options, cancellationToken);
    }

    public async Task<ParserIngestionResult> ValidateProductDetailsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        return await ScanProductDetailsRunAsync("validate-product-details", runDirectory, options, cancellationToken);
    }

    private async Task<ParserIngestionResult> ScanRankRunAsync(
        string mode,
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var manifest = LoadManifest(runDirectory, RanksKind);
        var summary = new ImportSummary(mode, manifest.ParserRunId, options.DryRun);

        await ScanRankSnapshotsAsync(
            RequiredFile(runDirectory, "product_rank_snapshots.jsonl"),
            manifest,
            options,
            summary,
            cancellationToken);
        await ScanRankPageFetchesAsync(
            RequiredFile(runDirectory, "rank_page_fetches.jsonl"),
            manifest,
            options,
            summary,
            cancellationToken);

        return summary.ToResult();
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
        await ScanReviewCoverageAsync(
            RequiredFile(runDirectory, "review_coverage.jsonl"),
            options,
            summary,
            cancellationToken);

        return summary.ToResult();
    }

    private async Task<ParserIngestionResult> ScanLogisticsRunAsync(
        string mode,
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var manifest = LoadManifest(runDirectory, LogisticsKind);
        var summary = new ImportSummary(mode, manifest.ParserRunId, options.DryRun);

        await ScanLogisticsSnapshotsAsync(
            RequiredFile(runDirectory, "logistics_snapshots.jsonl"),
            manifest,
            options,
            summary,
            cancellationToken);
        await ScanWarehouseAvailabilityAsync(
            RequiredFile(runDirectory, "warehouse_availability.jsonl"),
            manifest,
            options,
            summary,
            cancellationToken);

        return summary.ToResult();
    }

    private async Task<ParserIngestionResult> ScanProductDetailsRunAsync(
        string mode,
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var manifest = LoadManifest(runDirectory, ProductDetailsKind);
        var summary = new ImportSummary(mode, manifest.ParserRunId, options.DryRun);

        await ScanProductDetailsRowsAsync(
            RequiredFile(runDirectory, "product_details.jsonl"),
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
                "review_coverage.jsonl",
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
            await StageReviewCoverageAsync(
                RequiredFile(runDirectory, "review_coverage.jsonl"),
                options,
                summary,
                manifest.ParserRunId,
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

    public async Task<ParserIngestionResult> StageRanksAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var manifest = LoadManifest(runDirectory, RanksKind);
        if (options.DryRun)
            return await ScanRankRunAsync("stage-ranks", runDirectory, options, cancellationToken);

        var registered = await RegisterRunAndFilesAsync(
            runDirectory,
            manifest,
            [
                "manifest.json",
                "product_rank_snapshots.jsonl",
                "rank_page_fetches.jsonl",
                "errors.jsonl",
                "runner.log"
            ],
            cancellationToken);
        var execution = await StartExecutionAsync(registered.Run.Id, "stage-ranks", cancellationToken);
        var summary = new ImportSummary("stage-ranks", manifest.ParserRunId, isDryRun: false);

        try
        {
            await StageRankSnapshotsAsync(
                RequiredFile(runDirectory, "product_rank_snapshots.jsonl"),
                manifest,
                registered.Run,
                registered.Files["product_rank_snapshots.jsonl"],
                execution,
                options,
                summary,
                cancellationToken);
            await StageRankPageFetchesAsync(
                RequiredFile(runDirectory, "rank_page_fetches.jsonl"),
                manifest,
                registered.Run,
                registered.Files["rank_page_fetches.jsonl"],
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

    public async Task<ParserIngestionResult> StageLogisticsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var manifest = LoadManifest(runDirectory, LogisticsKind);
        if (options.DryRun)
            return await ScanLogisticsRunAsync("stage-logistics", runDirectory, options, cancellationToken);

        var registered = await RegisterRunAndFilesAsync(
            runDirectory,
            manifest,
            [
                "manifest.json",
                "logistics_snapshots.jsonl",
                "warehouse_availability.jsonl",
                "errors.jsonl",
                "runner.log"
            ],
            cancellationToken);
        var execution = await StartExecutionAsync(registered.Run.Id, "stage-logistics", cancellationToken);
        var summary = new ImportSummary("stage-logistics", manifest.ParserRunId, isDryRun: false);

        try
        {
            await StageLogisticsSnapshotsAsync(
                RequiredFile(runDirectory, "logistics_snapshots.jsonl"),
                manifest,
                registered.Run,
                registered.Files["logistics_snapshots.jsonl"],
                execution,
                options,
                summary,
                cancellationToken);
            await StageWarehouseAvailabilityAsync(
                RequiredFile(runDirectory, "warehouse_availability.jsonl"),
                manifest,
                registered.Run,
                registered.Files["warehouse_availability.jsonl"],
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

    public async Task<ParserIngestionResult> StageProductDetailsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var manifest = LoadManifest(runDirectory, ProductDetailsKind);
        if (options.DryRun)
            return await ScanProductDetailsRunAsync("stage-product-details", runDirectory, options, cancellationToken);

        var registered = await RegisterRunAndFilesAsync(
            runDirectory,
            manifest,
            [
                "manifest.json",
                "product_details.jsonl",
                "product_detail_fetch_results.jsonl",
                "errors.jsonl",
                "runner.log"
            ],
            cancellationToken);
        var execution = await StartExecutionAsync(registered.Run.Id, "stage-product-details", cancellationToken);
        var summary = new ImportSummary("stage-product-details", manifest.ParserRunId, isDryRun: false);

        try
        {
            await StageProductDetailsRowsAsync(
                RequiredFile(runDirectory, "product_details.jsonl"),
                manifest,
                registered.Run,
                registered.Files["product_details.jsonl"],
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

    public async Task<ParserIngestionResult> StageCompleteBatchAsync(
        string batchDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        var batch = LoadBatchManifest(batchDirectory);
        var summary = new ImportSummary("stage-complete-batch", batch.BatchId, options.DryRun);
        summary.Increment("batch_size", batch.Size);

        if (options.DryRun)
        {
            summary.Absorb(await StageProductsAsync(batch.ProductRunDirectory, options, cancellationToken));
            foreach (var runDirectory in batch.LogisticsRunDirectories)
                summary.Absorb(await StageLogisticsAsync(runDirectory, options, cancellationToken));
            foreach (var runDirectory in batch.ReviewRunDirectories)
                summary.Absorb(await StageReviewsAsync(runDirectory, options, cancellationToken));
            foreach (var runDirectory in ExistingProductDetailsRunDirectories(batch.ProductDetailsRunDirectories))
                summary.Absorb(await StageProductDetailsAsync(runDirectory, options, cancellationToken));
            summary.Increment("promote-products_skipped_dry_run");
            return summary.ToResult();
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        summary.Absorb(await StageProductsAsync(batch.ProductRunDirectory, options, cancellationToken));
        foreach (var runDirectory in batch.LogisticsRunDirectories)
            summary.Absorb(await StageLogisticsAsync(runDirectory, options, cancellationToken));
        foreach (var runDirectory in batch.ReviewRunDirectories)
            summary.Absorb(await StageReviewsAsync(runDirectory, options, cancellationToken));
        foreach (var runDirectory in ExistingProductDetailsRunDirectories(batch.ProductDetailsRunDirectories))
            summary.Absorb(await StageProductDetailsAsync(runDirectory, options, cancellationToken));
        await RejectDefectiveBatchCardsAsync(batch, summary, cancellationToken);
        summary.Absorb(await PromoteProductsAsync(batch.ProductParserRunId, options, cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return summary.ToResult();
    }

    private async Task RejectDefectiveBatchCardsAsync(
        BatchManifestInfo batch,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var productRun = await _dbContext.ParserRuns
            .FirstOrDefaultAsync(x => x.ParserRunId == batch.ProductParserRunId && x.Kind == ProductsKind, cancellationToken);
        if (productRun is null)
            throw new InvalidOperationException($"Product parser run '{batch.ProductParserRunId}' is not staged.");

        var productRows = await _dbContext.ParserProductRows
            .Where(x => x.IdParserRun == productRun.Id)
            .OrderBy(x => x.ParsedAtUtc)
            .ThenBy(x => x.SourceLineNumber)
            .ToListAsync(cancellationToken);
        if (productRows.Count == 0)
            return;

        var productIds = productRows.Select(x => x.WbProductId).Distinct().ToList();
        var rootIds = productRows
            .Select(x => x.WbRootId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .ToList();
        var detailRunIds = await StagedRunIdsFromDirectoriesAsync(batch.ProductDetailsRunDirectories, ProductDetailsKind, cancellationToken);
        var logisticsRunIds = await StagedRunIdsFromDirectoriesAsync(batch.LogisticsRunDirectories, LogisticsRunKind, cancellationToken);
        var reviewRunIds = await StagedRunIdsFromDirectoriesAsync(batch.ReviewRunDirectories, ReviewsKind, cancellationToken);

        var productsWithSuccessfulDetails = await _dbContext.ParserProductDetailRows
            .AsNoTracking()
            .Where(x => detailRunIds.Contains(x.IdParserRun)
                        && productIds.Contains(x.WbProductId)
                        && (x.Status.ToLower() == "success" || x.Status.ToLower() == "succeeded"))
            .Select(x => x.WbProductId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var productsWithLogistics = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x => logisticsRunIds.Contains(x.IdParserRun) && productIds.Contains(x.WbProductId))
            .Select(x => x.WbProductId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var rootsWithReviewFetch = await _dbContext.ParserReviewRootFetches
            .AsNoTracking()
            .Where(x => reviewRunIds.Contains(x.IdParserRun)
                        && rootIds.Contains(x.SourceWbRootId)
                        && (x.Status.ToLower() == "success"
                            || x.Status.ToLower() == "succeeded"
                            || x.Status.ToLower() == "empty"))
            .Select(x => x.SourceWbRootId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var detailSet = productsWithSuccessfulDetails.ToHashSet(StringComparer.Ordinal);
        var logisticsSet = productsWithLogistics.ToHashSet(StringComparer.Ordinal);
        var reviewFetchSet = rootsWithReviewFetch.ToHashSet(StringComparer.Ordinal);
        var rejected = new List<(ParserProductRow Row, ParserDefectiveCardAssessment Assessment)>();

        foreach (var row in productRows)
        {
            var evidence = new ParserCardCompletenessEvidence(
                HasSuccessfulProductDetails: detailSet.Contains(row.WbProductId),
                HasLogisticsAttempt: logisticsSet.Contains(row.WbProductId),
                HasReviewFetchAttempt: !string.IsNullOrWhiteSpace(row.WbRootId) && reviewFetchSet.Contains(row.WbRootId));
            var assessment = ParserDefectiveCardDetector.Evaluate(row, evidence);
            if (assessment.IsDefective)
                rejected.Add((row, assessment));
        }

        if (rejected.Count == 0)
            return;

        var execution = await StartExecutionAsync(productRun.Id, "reject-defective-cards", cancellationToken);
        var rejectionSummary = new ImportSummary("reject-defective-cards", batch.BatchId, isDryRun: false);
        var rejectedProductIds = rejected.Select(x => x.Row.WbProductId).Distinct().ToList();
        var rejectedRootIds = rejected
            .Select(x => x.Row.WbRootId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .ToList();
        var rejectedProductRowIds = rejected.Select(x => x.Row.Id).ToHashSet();

        foreach (var (row, assessment) in rejected)
        {
            summary.Increment("defective_cards_rejected");
            summary.RowsSkipped++;
            summary.Errors++;
            rejectionSummary.Increment("defective_cards_rejected");
            rejectionSummary.RowsSkipped++;
            rejectionSummary.Errors++;
            foreach (var reason in assessment.Reasons)
            {
                summary.Increment($"defective:{reason}");
                rejectionSummary.Increment($"defective:{reason}");
            }

            _dbContext.ParserImportErrors.Add(CreateError(
                execution.Id,
                row.IdParserRun,
                row.IdParserFile,
                row.SourceLineNumber,
                "defective-card",
                $"Parser product '{row.WbProductId}' rejected as defective: {string.Join(", ", assessment.Reasons)}"));
        }

        await DeleteRejectedBatchRowsAsync(
            rejectedProductIds,
            rejectedRootIds,
            rejectedProductRowIds,
            productRun.Id,
            detailRunIds,
            logisticsRunIds,
            reviewRunIds,
            cancellationToken);
        await FinishExecutionAsync(execution, rejectionSummary, "succeeded", cancellationToken);
    }

    private async Task<HashSet<Guid>> StagedRunIdsFromDirectoriesAsync(
        IReadOnlyList<string> runDirectories,
        string expectedKind,
        CancellationToken cancellationToken)
    {
        if (runDirectories.Count == 0)
            return [];

        var parserRunIds = runDirectories
            .Select(path => LoadManifest(path, expectedKind).ParserRunId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var ids = await _dbContext.ParserRuns
            .AsNoTracking()
            .Where(x => parserRunIds.Contains(x.ParserRunId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    private async Task DeleteRejectedBatchRowsAsync(
        IReadOnlyList<string> rejectedProductIds,
        IReadOnlyList<string> rejectedRootIds,
        IReadOnlySet<Guid> rejectedProductRowIds,
        Guid productRunId,
        IReadOnlySet<Guid> detailRunIds,
        IReadOnlySet<Guid> logisticsRunIds,
        IReadOnlySet<Guid> reviewRunIds,
        CancellationToken cancellationToken)
    {
        if (rejectedProductIds.Count == 0)
            return;

        var rootFetchIds = rejectedRootIds.Count == 0 || reviewRunIds.Count == 0
            ? []
            : await _dbContext.ParserReviewRootFetches
                .Where(x => reviewRunIds.Contains(x.IdParserRun) && rejectedRootIds.Contains(x.SourceWbRootId))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

        if (rejectedProductRowIds.Count > 0)
        {
            await _dbContext.MarketHotProductRecommendations
                .Where(x => x.IdParserProductRow.HasValue && rejectedProductRowIds.Contains(x.IdParserProductRow.Value))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (reviewRunIds.Count > 0)
        {
            await _dbContext.ParserReviewReplyRows
                .Where(x => reviewRunIds.Contains(x.IdParserRun)
                            && (rejectedProductIds.Contains(x.WbProductId)
                                || (x.IdReviewRootFetch.HasValue && rootFetchIds.Contains(x.IdReviewRootFetch.Value))))
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.ParserReviewRows
                .Where(x => reviewRunIds.Contains(x.IdParserRun)
                            && (rejectedProductIds.Contains(x.WbProductId)
                                || (x.IdReviewRootFetch.HasValue && rootFetchIds.Contains(x.IdReviewRootFetch.Value))))
                .ExecuteDeleteAsync(cancellationToken);
            if (rootFetchIds.Count > 0)
            {
                await _dbContext.ParserReviewRootFetches
                    .Where(x => rootFetchIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        if (detailRunIds.Count > 0)
        {
            await _dbContext.ParserProductDetailRows
                .Where(x => detailRunIds.Contains(x.IdParserRun) && rejectedProductIds.Contains(x.WbProductId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (logisticsRunIds.Count > 0)
        {
            await _dbContext.ParserWarehouseAvailabilityRows
                .Where(x => logisticsRunIds.Contains(x.IdParserRun) && rejectedProductIds.Contains(x.WbProductId))
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.ParserLogisticsSnapshotRows
                .Where(x => logisticsRunIds.Contains(x.IdParserRun) && rejectedProductIds.Contains(x.WbProductId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await _dbContext.ParserProductRows
            .Where(x => x.IdParserRun == productRunId && rejectedProductIds.Contains(x.WbProductId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<ParserIngestionResult> CompleteParserPipelineAsync(
        string pipelineRunId,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pipelineRunId))
            throw new ArgumentException("Pipeline run id is required.", nameof(pipelineRunId));

        var summary = new ImportSummary("complete-parser-pipeline", pipelineRunId, options.DryRun);
        var runs = await _dbContext.ParserRuns
            .AsNoTracking()
            .Where(x => x.RequestedScope != null)
            .ToListAsync(cancellationToken);
        var pipelineRuns = runs
            .Where(x => ScopeString(x.RequestedScope, "pipeline_run_id") == pipelineRunId)
            .ToList();
        var shardKey = pipelineRuns
            .Select(x => ScopeString(x.RequestedScope, "shard_key"))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var productRunIds = pipelineRuns
            .Where(x => x.Kind == ProductsKind)
            .Select(x => x.Id)
            .ToList();
        var discoveredProductIds = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => productRunIds.Contains(x.IdParserRun))
            .Select(x => x.WbProductId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var currentProductIds = discoveredProductIds.ToHashSet(StringComparer.Ordinal);

        var previousPipelineId = runs
            .Where(x => x.Kind == ProductsKind && !ScopeBool(x.RequestedScope, "is_test_run"))
            .Select(x => new
            {
                Run = x,
                PipelineRunId = ScopeString(x.RequestedScope, "pipeline_run_id"),
                ShardKey = ScopeString(x.RequestedScope, "shard_key")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.PipelineRunId)
                        && x.PipelineRunId != pipelineRunId
                        && (string.IsNullOrWhiteSpace(shardKey) || x.ShardKey == shardKey))
            .OrderByDescending(x => x.Run.FinishedAtUtc ?? x.Run.StartedAtUtc)
            .Select(x => x.PipelineRunId)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(previousPipelineId))
        {
            var previousProductRunIds = runs
                .Where(x => x.Kind == ProductsKind
                            && ScopeString(x.RequestedScope, "pipeline_run_id") == previousPipelineId)
                .Select(x => x.Id)
                .ToList();
            var previousProductIds = await _dbContext.ParserProductRows
                .AsNoTracking()
                .Where(x => previousProductRunIds.Contains(x.IdParserRun))
                .Select(x => x.WbProductId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var previousSet = previousProductIds.ToHashSet(StringComparer.Ordinal);
            summary.Increment("previous_discovered_products", previousSet.Count);
            summary.Increment("new_products", currentProductIds.Count(x => !previousSet.Contains(x)));
            summary.Increment("disappeared_products", previousSet.Count(x => !currentProductIds.Contains(x)));
        }
        else
        {
            summary.Increment("previous_discovered_products", 0);
            summary.Increment("new_products", currentProductIds.Count);
            summary.Increment("disappeared_products", 0);
        }

        summary.RowsRead = discoveredProductIds.Count;
        summary.Increment("parser_runs", pipelineRuns.Count);
        summary.Increment("product_runs", productRunIds.Count);
        summary.Increment("discovered_products", discoveredProductIds.Count);
        summary.Increment("complete_card_batches", pipelineRuns.Count(x => ScopeBool(x.RequestedScope, "is_complete_card_batch")));
        if (!string.IsNullOrWhiteSpace(shardKey))
            summary.Increment($"shard:{shardKey}", 1);
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
                var marketplaces = await _dbContext.Marketplaces
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
                var marketplace = marketplaces
                    .FirstOrDefault(x => ParserMarketplaceResolver.IsMatch(x, marketplaceRows.Key));
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
                            if (!options.DryRun)
                            {
                                var product = ParserProductPromotionMapper.CreateDomainProduct(row, marketplace.Id);
                                _dbContext.Products.Add(product);
                            }

                            summary.RowsWritten++;
                            summary.Increment("domain_products_created");
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
                        summary.Increment("domain_products_updated");
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
                await FlushProductBatchAsync(file.Id, rows, errors, summary, options, cancellationToken);
        }

        await FlushProductBatchAsync(file.Id, rows, errors, summary, options, cancellationToken);
        summary.Increment("product_rows_staged", summary.RowsWritten);
    }

    private async Task FlushProductBatchAsync(
        Guid fileId,
        List<ParserProductRow> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        ParserIngestionOptions options,
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
        await _cdcService.ApplyProductRowsAsync(newRows, BatchId(newRows), options.CdcContext, cancellationToken);
        _dbContext.ChangeTracker.Clear();
        rows.Clear();
        errors.Clear();
    }

    private async Task ScanLogisticsSnapshotsAsync(
        string path,
        ManifestInfo manifest,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var validRows = 0L;
        var invalidRows = 0L;
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                using var row = ParseLogisticsSnapshot(source, manifest, Guid.NewGuid(), Guid.NewGuid());
                summary.RowsWritten++;
                validRows++;
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
                invalidRows++;
            }
        }

        summary.Increment("logistics_snapshot_rows_valid", validRows);
        summary.Increment("logistics_snapshot_rows_invalid", invalidRows);
    }

    private async Task ScanWarehouseAvailabilityAsync(
        string path,
        ManifestInfo manifest,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var validRows = 0L;
        var invalidRows = 0L;
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                using var row = ParseWarehouseAvailability(source, manifest, Guid.NewGuid(), Guid.NewGuid());
                summary.RowsWritten++;
                validRows++;
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
                invalidRows++;
            }
        }

        summary.Increment("warehouse_availability_rows_valid", validRows);
        summary.Increment("warehouse_availability_rows_invalid", invalidRows);
    }

    private async Task StageLogisticsSnapshotsAsync(
        string path,
        ManifestInfo manifest,
        ParserRun run,
        ParserFile file,
        ParserImportExecution execution,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParserLogisticsSnapshotRow>(options.NormalizedBatchSize);
        var errors = new List<ParserImportError>();
        var writtenBefore = summary.RowsWritten;
        var skippedBefore = summary.RowsSkipped;
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                rows.Add(ParseLogisticsSnapshot(source, manifest, run.Id, file.Id));
            }
            catch (Exception exception)
            {
                summary.RowsSkipped++;
                summary.Errors++;
                summary.Increment("logistics_snapshot_rows_invalid");
                errors.Add(CreateError(execution.Id, run.Id, file.Id, source.LineNumber, "logistics-snapshot-row", exception.Message));
            }

            if (rows.Count + errors.Count >= options.NormalizedBatchSize)
                await FlushLogisticsSnapshotBatchAsync(file.Id, rows, errors, summary, options, cancellationToken);
        }

        await FlushLogisticsSnapshotBatchAsync(file.Id, rows, errors, summary, options, cancellationToken);
        summary.Increment("logistics_snapshot_rows_staged", summary.RowsWritten - writtenBefore);
        summary.Increment("logistics_snapshot_rows_skipped", summary.RowsSkipped - skippedBefore);
    }

    private async Task FlushLogisticsSnapshotBatchAsync(
        Guid fileId,
        List<ParserLogisticsSnapshotRow> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 && errors.Count == 0)
            return;

        var lineNumbers = rows.Select(x => x.SourceLineNumber).ToList();
        var existing = lineNumbers.Count == 0
            ? []
            : await _dbContext.ParserLogisticsSnapshotRows
                .AsNoTracking()
                .Where(x => x.IdParserFile == fileId && lineNumbers.Contains(x.SourceLineNumber))
                .Select(x => x.SourceLineNumber)
                .ToListAsync(cancellationToken);
        var existingLines = existing.ToHashSet();
        var newRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserLogisticsSnapshotRows.AddRange(newRows);
        _dbContext.ParserImportErrors.AddRange(errors);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cdcService.ApplyLogisticsRowsAsync(newRows, BatchId(newRows), options.CdcContext, cancellationToken);
        _dbContext.ChangeTracker.Clear();
        rows.Clear();
        errors.Clear();
    }

    private async Task StageWarehouseAvailabilityAsync(
        string path,
        ManifestInfo manifest,
        ParserRun run,
        ParserFile file,
        ParserImportExecution execution,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParserWarehouseAvailabilityRow>(options.NormalizedBatchSize);
        var errors = new List<ParserImportError>();
        var writtenBefore = summary.RowsWritten;
        var skippedBefore = summary.RowsSkipped;
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                rows.Add(ParseWarehouseAvailability(source, manifest, run.Id, file.Id));
            }
            catch (Exception exception)
            {
                summary.RowsSkipped++;
                summary.Errors++;
                summary.Increment("warehouse_availability_rows_invalid");
                errors.Add(CreateError(execution.Id, run.Id, file.Id, source.LineNumber, "warehouse-availability-row", exception.Message));
            }

            if (rows.Count + errors.Count >= options.NormalizedBatchSize)
                await FlushWarehouseAvailabilityBatchAsync(file.Id, rows, errors, summary, cancellationToken);
        }

        await FlushWarehouseAvailabilityBatchAsync(file.Id, rows, errors, summary, cancellationToken);
        summary.Increment("warehouse_availability_rows_staged", summary.RowsWritten - writtenBefore);
        summary.Increment("warehouse_availability_rows_skipped", summary.RowsSkipped - skippedBefore);
    }

    private async Task FlushWarehouseAvailabilityBatchAsync(
        Guid fileId,
        List<ParserWarehouseAvailabilityRow> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 && errors.Count == 0)
            return;

        var lineNumbers = rows.Select(x => x.SourceLineNumber).ToList();
        var existing = lineNumbers.Count == 0
            ? []
            : await _dbContext.ParserWarehouseAvailabilityRows
                .AsNoTracking()
                .Where(x => x.IdParserFile == fileId && lineNumbers.Contains(x.SourceLineNumber))
                .Select(x => x.SourceLineNumber)
                .ToListAsync(cancellationToken);
        var existingLines = existing.ToHashSet();
        var newRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserWarehouseAvailabilityRows.AddRange(newRows);
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

    private async Task ScanReviewCoverageAsync(
        string path,
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
                _ = ParseReviewCoverageSnapshot(source);
                summary.RowsWritten++;
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
            }
        }
    }

    private async Task StageReviewCoverageAsync(
        string path,
        ParserIngestionOptions options,
        ImportSummary summary,
        string batchId,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParserReviewCoverageSnapshot>(options.NormalizedBatchSize);
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                rows.Add(ParseReviewCoverageSnapshot(source));
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
            }

            if (rows.Count >= options.NormalizedBatchSize)
                await FlushReviewCoverageBatchAsync(rows, summary, batchId, options, cancellationToken);
        }

        await FlushReviewCoverageBatchAsync(rows, summary, batchId, options, cancellationToken);
    }

    private async Task FlushReviewCoverageBatchAsync(
        List<ParserReviewCoverageSnapshot> rows,
        ImportSummary summary,
        string batchId,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;

        await _cdcService.ApplyReviewCoverageRowsAsync(rows, batchId, options.CdcContext, cancellationToken);
        summary.RowsWritten += rows.Count;
        rows.Clear();
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
                await FlushReviewBatchAsync(file.Id, rows, errors, summary, options, cancellationToken);
        }

        await FlushReviewBatchAsync(file.Id, rows, errors, summary, options, cancellationToken);
    }

    private async Task FlushReviewBatchAsync(
        Guid fileId,
        List<ParserReviewRow> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        ParserIngestionOptions options,
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
        var candidateRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();
        var existingReviewKeys = await LoadExistingReviewKeysAsync(candidateRows, cancellationToken);
        var batchReviewKeys = new HashSet<ReviewIdentity>();
        var newRows = candidateRows
            .Where(row =>
            {
                var key = ToReviewIdentity(row);
                return !existingReviewKeys.Contains(key) && batchReviewKeys.Add(key);
            })
            .ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserReviewRows.AddRange(newRows);
        _dbContext.ParserImportErrors.AddRange(errors);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cdcService.ApplyReviewRowsAsync(candidateRows, BatchId(candidateRows), options.CdcContext, cancellationToken);
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
        var candidateRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();
        var existingReplyKeys = await LoadExistingReviewReplyKeysAsync(candidateRows, cancellationToken);
        var batchReplyKeys = new HashSet<ReviewReplyIdentity>();
        var newRows = candidateRows
            .Where(row =>
            {
                var key = ToReviewReplyIdentity(row);
                return !existingReplyKeys.Contains(key) && batchReplyKeys.Add(key);
            })
            .ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserReviewReplyRows.AddRange(newRows);
        _dbContext.ParserImportErrors.AddRange(errors);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        rows.Clear();
        errors.Clear();
    }

    private async Task<HashSet<ReviewIdentity>> LoadExistingReviewKeysAsync(
        IReadOnlyCollection<ParserReviewRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return [];

        var marketplaces = rows.Select(x => x.Marketplace).Distinct(StringComparer.Ordinal).ToList();
        var productIds = rows.Select(x => x.WbProductId).Distinct(StringComparer.Ordinal).ToList();
        var reviewIds = rows.Select(x => x.ReviewIdOnMp).Distinct(StringComparer.Ordinal).ToList();

        var existing = await _dbContext.ParserReviewRows
            .AsNoTracking()
            .Where(x =>
                marketplaces.Contains(x.Marketplace)
                && productIds.Contains(x.WbProductId)
                && reviewIds.Contains(x.ReviewIdOnMp))
            .Select(x => new { x.Marketplace, x.WbProductId, x.ReviewIdOnMp })
            .ToListAsync(cancellationToken);

        return existing
            .Select(x => new ReviewIdentity(x.Marketplace, x.WbProductId, x.ReviewIdOnMp))
            .ToHashSet();
    }

    private async Task<HashSet<ReviewReplyIdentity>> LoadExistingReviewReplyKeysAsync(
        IReadOnlyCollection<ParserReviewReplyRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return [];

        var marketplaces = rows.Select(x => x.Marketplace).Distinct(StringComparer.Ordinal).ToList();
        var productIds = rows.Select(x => x.WbProductId).Distinct(StringComparer.Ordinal).ToList();
        var reviewIds = rows.Select(x => x.ReviewIdOnMp).Distinct(StringComparer.Ordinal).ToList();
        var replyIds = rows
            .Select(x => x.ReplyIdOnMp)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var fallbackHashes = rows
            .Select(x => x.ReplyFallbackHash)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var existing = await _dbContext.ParserReviewReplyRows
            .AsNoTracking()
            .Where(x =>
                marketplaces.Contains(x.Marketplace)
                && productIds.Contains(x.WbProductId)
                && reviewIds.Contains(x.ReviewIdOnMp)
                && ((x.ReplyIdOnMp != null && replyIds.Contains(x.ReplyIdOnMp))
                    || (x.ReplyFallbackHash != null && fallbackHashes.Contains(x.ReplyFallbackHash))))
            .Select(x => new
            {
                x.Marketplace,
                x.WbProductId,
                x.ReviewIdOnMp,
                x.ReplyIdOnMp,
                x.ReplyFallbackHash
            })
            .ToListAsync(cancellationToken);

        return existing
            .Select(x => new ReviewReplyIdentity(
                x.Marketplace,
                x.WbProductId,
                x.ReviewIdOnMp,
                ReviewReplyKey(x.ReplyIdOnMp, x.ReplyFallbackHash)))
            .ToHashSet();
    }

    private static ReviewIdentity ToReviewIdentity(ParserReviewRow row) =>
        new(row.Marketplace, row.WbProductId, row.ReviewIdOnMp);

    private static ReviewReplyIdentity ToReviewReplyIdentity(ParserReviewReplyRow row) =>
        new(row.Marketplace, row.WbProductId, row.ReviewIdOnMp, ReviewReplyKey(row.ReplyIdOnMp, row.ReplyFallbackHash));

    private static string ReviewReplyKey(string? replyIdOnMp, string? replyFallbackHash) =>
        string.IsNullOrWhiteSpace(replyIdOnMp)
            ? $"fallback:{replyFallbackHash}"
            : $"id:{replyIdOnMp}";

    private async Task ScanRankSnapshotsAsync(
        string path,
        ManifestInfo manifest,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var validRows = 0L;
        var invalidRows = 0L;
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                using var row = ParseRankSnapshot(source, manifest, Guid.NewGuid(), Guid.NewGuid());
                summary.RowsWritten++;
                validRows++;
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
                invalidRows++;
            }
        }

        summary.Increment("rank_snapshot_rows_valid", validRows);
        summary.Increment("rank_snapshot_rows_invalid", invalidRows);
    }

    private async Task ScanRankPageFetchesAsync(
        string path,
        ManifestInfo manifest,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var validRows = 0L;
        var invalidRows = 0L;
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                using var row = ParseRankPageFetch(source, manifest, Guid.NewGuid(), Guid.NewGuid());
                summary.RowsWritten++;
                validRows++;
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
                invalidRows++;
            }
        }

        summary.Increment("rank_page_fetch_rows_valid", validRows);
        summary.Increment("rank_page_fetch_rows_invalid", invalidRows);
    }

    private async Task StageRankSnapshotsAsync(
        string path,
        ManifestInfo manifest,
        ParserRun run,
        ParserFile file,
        ParserImportExecution execution,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParserRankSnapshotRow>(options.NormalizedBatchSize);
        var errors = new List<ParserImportError>();
        var writtenBefore = summary.RowsWritten;
        var skippedBefore = summary.RowsSkipped;
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                rows.Add(ParseRankSnapshot(source, manifest, run.Id, file.Id));
            }
            catch (Exception exception)
            {
                summary.RowsSkipped++;
                summary.Errors++;
                summary.Increment("rank_snapshot_rows_invalid");
                errors.Add(CreateError(execution.Id, run.Id, file.Id, source.LineNumber, "rank-snapshot-row", exception.Message));
            }

            if (rows.Count + errors.Count >= options.NormalizedBatchSize)
                await FlushRankSnapshotBatchAsync(file.Id, rows, errors, summary, options, cancellationToken);
        }

        await FlushRankSnapshotBatchAsync(file.Id, rows, errors, summary, options, cancellationToken);
        summary.Increment("rank_snapshot_rows_staged", summary.RowsWritten - writtenBefore);
        summary.Increment("rank_snapshot_rows_skipped", summary.RowsSkipped - skippedBefore);
    }

    private async Task FlushRankSnapshotBatchAsync(
        Guid fileId,
        List<ParserRankSnapshotRow> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 && errors.Count == 0)
            return;

        var lineNumbers = rows.Select(x => x.SourceLineNumber).ToList();
        var existing = lineNumbers.Count == 0
            ? []
            : await _dbContext.ParserRankSnapshotRows
                .AsNoTracking()
                .Where(x => x.IdParserFile == fileId && lineNumbers.Contains(x.SourceLineNumber))
                .Select(x => x.SourceLineNumber)
                .ToListAsync(cancellationToken);
        var existingLines = existing.ToHashSet();
        var newRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserRankSnapshotRows.AddRange(newRows);
        _dbContext.ParserImportErrors.AddRange(errors);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cdcService.ApplyRankRowsAsync(newRows, BatchId(newRows), options.CdcContext, cancellationToken);
        _dbContext.ChangeTracker.Clear();
        rows.Clear();
        errors.Clear();
    }

    private async Task StageRankPageFetchesAsync(
        string path,
        ManifestInfo manifest,
        ParserRun run,
        ParserFile file,
        ParserImportExecution execution,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParserRankPageFetch>(options.NormalizedBatchSize);
        var errors = new List<ParserImportError>();
        var writtenBefore = summary.RowsWritten;
        var skippedBefore = summary.RowsSkipped;
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                rows.Add(ParseRankPageFetch(source, manifest, run.Id, file.Id));
            }
            catch (Exception exception)
            {
                summary.RowsSkipped++;
                summary.Errors++;
                summary.Increment("rank_page_fetch_rows_invalid");
                errors.Add(CreateError(execution.Id, run.Id, file.Id, source.LineNumber, "rank-page-fetch-row", exception.Message));
            }

            if (rows.Count + errors.Count >= options.NormalizedBatchSize)
                await FlushRankPageFetchBatchAsync(file.Id, rows, errors, summary, cancellationToken);
        }

        await FlushRankPageFetchBatchAsync(file.Id, rows, errors, summary, cancellationToken);
        summary.Increment("rank_page_fetch_rows_staged", summary.RowsWritten - writtenBefore);
        summary.Increment("rank_page_fetch_rows_skipped", summary.RowsSkipped - skippedBefore);
    }

    private async Task FlushRankPageFetchBatchAsync(
        Guid fileId,
        List<ParserRankPageFetch> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 && errors.Count == 0)
            return;

        var lineNumbers = rows.Select(x => x.SourceLineNumber).ToList();
        var existing = lineNumbers.Count == 0
            ? []
            : await _dbContext.ParserRankPageFetches
                .AsNoTracking()
                .Where(x => x.IdParserFile == fileId && lineNumbers.Contains(x.SourceLineNumber))
                .Select(x => x.SourceLineNumber)
                .ToListAsync(cancellationToken);
        var existingLines = existing.ToHashSet();
        var newRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserRankPageFetches.AddRange(newRows);
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

    private BatchManifestInfo LoadBatchManifest(string batchDirectory)
    {
        if (string.IsNullOrWhiteSpace(batchDirectory))
            throw new ArgumentException("Batch directory is required.", nameof(batchDirectory));

        var manifestPath = Path.Combine(batchDirectory, "batch_manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Required parser batch artifact 'batch_manifest.json' was not found.", manifestPath);

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var productRunDirectory = ResolveArtifactPath(
            batchDirectory,
            RequiredString(root, "product_run_dir"));
        var productParserRunId = LoadManifest(productRunDirectory, ProductsKind).ParserRunId;

        var logistics = StepOutputRunDirectories(root, batchDirectory, "logistics");
        var reviews = StepOutputRunDirectories(root, batchDirectory, "reviews");
        var productDetails = StepOutputRunDirectories(root, batchDirectory, "product_details");
        if (logistics.Count == 0)
            throw new InvalidDataException("Complete batch manifest does not contain logistics output run directories.");
        if (reviews.Count == 0)
            throw new InvalidDataException("Complete batch manifest does not contain reviews output run directories.");
        return new BatchManifestInfo(
            RequiredString(root, "batch_id"),
            ReadInt(root, "size") ?? 0,
            productRunDirectory,
            productParserRunId,
            logistics,
            reviews,
            productDetails);
    }

    private static IEnumerable<string> ExistingProductDetailsRunDirectories(IEnumerable<string> runDirectories)
    {
        foreach (var runDirectory in runDirectories)
        {
            if (File.Exists(Path.Combine(runDirectory, "product_details.jsonl")))
                yield return runDirectory;
        }
    }

    private static List<string> StepOutputRunDirectories(JsonElement root, string batchDirectory, string stepName)
    {
        if (!root.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
            return [];

        var directories = new List<string>();
        foreach (var step in steps.EnumerateArray())
        {
            if (!string.Equals(ReadString(step, "step"), stepName, StringComparison.Ordinal))
                continue;
            if (!step.TryGetProperty("output_run_dirs", out var outputRunDirs) || outputRunDirs.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in outputRunDirs.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    continue;
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    directories.Add(ResolveArtifactPath(batchDirectory, value));
            }
        }

        return directories;
    }

    private static string ResolveArtifactPath(string baseDirectory, string path)
    {
        return Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectory, path));
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
        ParserProductPromotionMapper.ApplyParserOwnedUpdates(product, row);
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

    private async Task ScanProductDetailsRowsAsync(
        string path,
        ManifestInfo manifest,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var validRows = 0L;
        var invalidRows = 0L;
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                using var row = ParseProductDetailRow(source, manifest, Guid.NewGuid(), Guid.NewGuid());
                summary.RowsWritten++;
                validRows++;
            }
            catch
            {
                summary.RowsSkipped++;
                summary.Errors++;
                invalidRows++;
            }
        }

        summary.Increment("product_detail_rows_valid", validRows);
        summary.Increment("product_detail_rows_invalid", invalidRows);
    }

    private async Task StageProductDetailsRowsAsync(
        string path,
        ManifestInfo manifest,
        ParserRun run,
        ParserFile file,
        ParserImportExecution execution,
        ParserIngestionOptions options,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParserProductDetailRow>(options.NormalizedBatchSize);
        var errors = new List<ParserImportError>();
        var writtenBefore = summary.RowsWritten;
        var skippedBefore = summary.RowsSkipped;
        await foreach (var source in ReadJsonLinesAsync(path, options.MaxRowsPerFile, cancellationToken))
        {
            using var sourceScope = source;
            summary.RowsRead++;
            try
            {
                rows.Add(ParseProductDetailRow(source, manifest, run.Id, file.Id));
            }
            catch (Exception exception)
            {
                summary.RowsSkipped++;
                summary.Errors++;
                summary.Increment("product_detail_rows_invalid");
                errors.Add(CreateError(execution.Id, run.Id, file.Id, source.LineNumber, "product-detail-row", exception.Message));
            }

            if (rows.Count + errors.Count >= options.NormalizedBatchSize)
                await FlushProductDetailBatchAsync(file.Id, rows, errors, summary, options, cancellationToken);
        }

        await FlushProductDetailBatchAsync(file.Id, rows, errors, summary, options, cancellationToken);
        summary.Increment("product_detail_rows_staged", summary.RowsWritten - writtenBefore);
        summary.Increment("product_detail_rows_skipped", summary.RowsSkipped - skippedBefore);
    }

    private async Task FlushProductDetailBatchAsync(
        Guid fileId,
        List<ParserProductDetailRow> rows,
        List<ParserImportError> errors,
        ImportSummary summary,
        ParserIngestionOptions options,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 && errors.Count == 0)
            return;

        var lineNumbers = rows.Select(x => x.SourceLineNumber).ToList();
        var existing = lineNumbers.Count == 0
            ? []
            : await _dbContext.ParserProductDetailRows
                .AsNoTracking()
                .Where(x => x.IdParserFile == fileId && lineNumbers.Contains(x.SourceLineNumber))
                .Select(x => x.SourceLineNumber)
                .ToListAsync(cancellationToken);
        var existingLines = existing.ToHashSet();
        var newRows = rows.Where(x => !existingLines.Contains(x.SourceLineNumber)).ToList();

        summary.RowsSkipped += rows.Count - newRows.Count;
        summary.RowsWritten += newRows.Count;
        _dbContext.ParserProductDetailRows.AddRange(newRows);
        _dbContext.ParserImportErrors.AddRange(errors);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cdcService.ApplyProductDetailRowsAsync(newRows, BatchId(newRows), options.CdcContext, cancellationToken);
        _dbContext.ChangeTracker.Clear();
        rows.Clear();
        errors.Clear();
    }

    private static string BatchId(IReadOnlyCollection<ParserProductRow> rows) =>
        rows.FirstOrDefault()?.ParserRunId ?? "unknown";

    private static string BatchId(IReadOnlyCollection<ParserLogisticsSnapshotRow> rows) =>
        rows.FirstOrDefault()?.ParserRunId ?? "unknown";

    private static string BatchId(IReadOnlyCollection<ParserReviewRow> rows) =>
        rows.FirstOrDefault()?.ParserRunId ?? "unknown";

    private static string BatchId(IReadOnlyCollection<ParserRankSnapshotRow> rows) =>
        rows.FirstOrDefault()?.ParserRunId ?? "unknown";

    private static string BatchId(IReadOnlyCollection<ParserProductDetailRow> rows) =>
        rows.FirstOrDefault()?.ParserRunId ?? "unknown";

    private static ManifestInfo LoadManifest(string runDirectory, string kind)
    {
        var path = RequiredFile(runDirectory, "manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        var root = manifest.RootElement;
        var runKind = ReadString(root, "run_kind");
        if (kind == LogisticsKind && !string.Equals(runKind, LogisticsRunKind, StringComparison.Ordinal))
            throw new InvalidDataException($"Logistics parser manifest must have run_kind '{LogisticsRunKind}'.");
        if (kind == ProductDetailsKind && !string.Equals(runKind, ProductDetailsKind, StringComparison.Ordinal))
            throw new InvalidDataException($"Product details parser manifest must have run_kind '{ProductDetailsKind}'.");

        var countersName = kind is ProductsKind or RanksKind ? "row_counts" : "counters";
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

    private static ParserProductDetailRow ParseProductDetailRow(
        JsonLine source,
        ManifestInfo manifest,
        Guid runId,
        Guid fileId)
    {
        source.ThrowIfInvalid();
        var row = source.Payload.RootElement;
        return new ParserProductDetailRow(
            runId,
            fileId,
            source.LineNumber,
            RowHash(source.RawLine),
            ReadInt(row, "schema_version") ?? 1,
            RequiredString(row, "parser_run_id"),
            RequiredUtcDate(row, "parsed_at_utc"),
            RequiredString(row, "marketplace"),
            ReadString(row, "input_products_parser_run_id"),
            ReadString(row, "input_products_jsonl"),
            RequiredString(row, "source_request_family"),
            RequiredString(row, "source_endpoint"),
            RequiredString(row, "request_fingerprint"),
            RequiredString(row, "wb_product_id"),
            ReadString(row, "wb_root_id"),
            ReadString(row, "source_category"),
            ReadString(row, "source_subcategory"),
            ReadString(row, "source_query"),
            ReadString(row, "source_region_dest"),
            ReadString(row, "description"),
            CloneDocument(CloneElement(row, "characteristics")),
            CloneDocument(CloneElement(row, "grouped_options")),
            ReadInt(row, "media_count"),
            RequiredString(row, "status"),
            CloneDocument(CloneElement(row, "raw_detail_fields")));
    }

    private static ParserRankSnapshotRow ParseRankSnapshot(
        JsonLine source,
        ManifestInfo manifest,
        Guid runId,
        Guid fileId)
    {
        source.ThrowIfInvalid();
        var row = source.Payload.RootElement;
        return new ParserRankSnapshotRow(
            runId,
            fileId,
            source.LineNumber,
            RowHash(source.RawLine),
            ReadInt(row, "schema_version") ?? 1,
            RequiredString(row, "parser_run_id"),
            RequiredUtcDate(row, "observed_at_utc"),
            RequiredString(row, "marketplace"),
            RequiredString(row, "rank_context_id"),
            RequiredString(row, "rank_context_type"),
            ReadString(row, "source_category"),
            ReadString(row, "source_subcategory"),
            RequiredString(row, "query"),
            ReadString(row, "source_region_dest"),
            ReadString(row, "sort"),
            CloneDocument(CloneElement(row, "filters")),
            RequiredString(row, "request_fingerprint"),
            RequiredPositiveInt(row, "page"),
            RequiredPositiveInt(row, "position_on_page"),
            RequiredPositiveInt(row, "absolute_position"),
            RequiredString(row, "wb_product_id"),
            ReadString(row, "wb_root_id"),
            ReadNonNegativeInt(row, "response_total"),
            RequiredString(row, "fetch_status"));
    }

    private static ParserRankPageFetch ParseRankPageFetch(
        JsonLine source,
        ManifestInfo manifest,
        Guid runId,
        Guid fileId)
    {
        source.ThrowIfInvalid();
        var row = source.Payload.RootElement;
        return new ParserRankPageFetch(
            runId,
            fileId,
            source.LineNumber,
            RowHash(source.RawLine),
            ReadInt(row, "schema_version") ?? 1,
            RequiredString(row, "parser_run_id"),
            RequiredUtcDate(row, "observed_at_utc"),
            manifest.Marketplace,
            RequiredString(row, "rank_context_id"),
            RequiredString(row, "rank_context_type"),
            ReadString(row, "source_category"),
            ReadString(row, "source_subcategory"),
            RequiredString(row, "query"),
            ReadString(row, "source_region_dest"),
            ReadString(row, "sort"),
            CloneDocument(CloneElement(row, "filters")),
            RequiredString(row, "request_fingerprint"),
            RequiredPositiveInt(row, "page"),
            RequiredString(row, "status"),
            RequiredNonNegativeInt(row, "product_count"),
            ReadNonNegativeInt(row, "response_total"),
            RequiredNonNegativeInt(row, "retry_count"),
            ReadNonNegativeInt(row, "http_status"),
            ReadString(row, "wb_code"),
            ReadString(row, "message"));
    }

    private static ParserLogisticsSnapshotRow ParseLogisticsSnapshot(
        JsonLine source,
        ManifestInfo manifest,
        Guid runId,
        Guid fileId)
    {
        source.ThrowIfInvalid();
        var row = source.Payload.RootElement;
        return new ParserLogisticsSnapshotRow(
            runId,
            fileId,
            source.LineNumber,
            RowHash(source.RawLine),
            ReadInt(row, "schema_version") ?? 1,
            RequiredString(row, "parser_run_id"),
            RequiredString(row, "marketplace"),
            RequiredUtcDate(row, "observed_at_utc"),
            RequiredString(row, "source_request_family"),
            RequiredString(row, "source_endpoint"),
            RequiredString(row, "request_fingerprint"),
            RequiredString(row, "source_region_dest"),
            ReadString(row, "delivery_profile_key"),
            ReadString(row, "delivery_destination_name"),
            ReadString(row, "delivery_profile_version"),
            ReadString(row, "delivery_destination_city"),
            ReadString(row, "delivery_destination_label"),
            ReadString(row, "delivery_destination_address"),
            ReadDecimal(row, "delivery_destination_latitude"),
            ReadDecimal(row, "delivery_destination_longitude"),
            ReadString(row, "source_category"),
            ReadString(row, "source_subcategory"),
            ReadString(row, "source_query"),
            RequiredString(row, "wb_product_id"),
            ReadString(row, "wb_root_id"),
            ReadString(row, "seller_id"),
            ReadString(row, "seller_name"),
            ReadInt(row, "total_quantity_observed"),
            ReadBool(row, "quantity_is_capped"),
            ReadInt(row, "quantity_cap_observed"),
            RequiredString(row, "quantity_semantics"),
            ReadString(row, "product_wh_raw"),
            ReadInt(row, "product_time1_raw"),
            ReadInt(row, "product_time2_raw"),
            ReadLong(row, "product_dtype_raw"),
            ReadInt(row, "product_dist_raw"),
            ReadString(row, "visible_delivery_status"),
            ReadString(row, "visible_delivery_label"),
            OptionalUtcDate(row, "visible_delivery_date"),
            ReadString(row, "visible_delivery_source"),
            OptionalUtcDate(row, "visible_delivery_observed_at_utc"),
            CloneDocument(CloneElement(row, "visible_delivery_raw_payload")),
            CloneDocument(CloneElement(row, "raw_observed_fields")));
    }

    private static ParserWarehouseAvailabilityRow ParseWarehouseAvailability(
        JsonLine source,
        ManifestInfo manifest,
        Guid runId,
        Guid fileId)
    {
        source.ThrowIfInvalid();
        var row = source.Payload.RootElement;
        return new ParserWarehouseAvailabilityRow(
            runId,
            fileId,
            source.LineNumber,
            RowHash(source.RawLine),
            ReadInt(row, "schema_version") ?? 1,
            RequiredString(row, "parser_run_id"),
            RequiredString(row, "marketplace"),
            RequiredUtcDate(row, "observed_at_utc"),
            RequiredString(row, "source_request_family"),
            RequiredString(row, "source_endpoint"),
            RequiredString(row, "request_fingerprint"),
            RequiredString(row, "source_region_dest"),
            ReadString(row, "delivery_profile_key"),
            ReadString(row, "delivery_destination_name"),
            ReadString(row, "delivery_profile_version"),
            ReadString(row, "delivery_destination_city"),
            ReadString(row, "delivery_destination_label"),
            ReadString(row, "delivery_destination_address"),
            ReadDecimal(row, "delivery_destination_latitude"),
            ReadDecimal(row, "delivery_destination_longitude"),
            ReadString(row, "source_category"),
            ReadString(row, "source_subcategory"),
            ReadString(row, "source_query"),
            RequiredString(row, "wb_product_id"),
            ReadString(row, "wb_root_id"),
            ReadString(row, "seller_id"),
            ReadString(row, "seller_name"),
            ReadString(row, "option_id"),
            ReadString(row, "size_name"),
            ReadString(row, "size_orig_name"),
            ReadInt(row, "size_rank"),
            ReadString(row, "warehouse_id_on_mp"),
            ReadInt(row, "quantity_observed"),
            ReadBool(row, "quantity_is_capped"),
            ReadInt(row, "quantity_cap_observed"),
            RequiredString(row, "quantity_semantics"),
            ReadInt(row, "stock_priority_raw"),
            ReadInt(row, "stock_time1_raw"),
            ReadInt(row, "stock_time2_raw"),
            ReadLong(row, "stock_dtype_raw"),
            ReadInt(row, "stock_dist_raw"),
            ReadDecimal(row, "price_basic"),
            ReadDecimal(row, "price_product"),
            ReadDecimal(row, "price_logistics_raw"),
            ReadDecimal(row, "price_return_raw"),
            CloneDocument(CloneElement(row, "raw_stock")),
            CloneDocument(CloneElement(row, "raw_size_observed_fields")));
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

    private static ParserReviewCoverageSnapshot ParseReviewCoverageSnapshot(JsonLine source)
    {
        source.ThrowIfInvalid();
        var row = source.Payload.RootElement;
        return new ParserReviewCoverageSnapshot(
            RequiredString(row, "source_wb_product_id"),
            ReadString(row, "source_wb_root_id"),
            ReadInt(row, "marketplace_feedback_count"),
            ReadInt(row, "fetched_reviews_count") ?? 0,
            RequiredString(row, "coverage_status"),
            RequiredString(row, "coverage_source"),
            ReadString(row, "last_coverage_error"),
            RequiredUtcDate(row, "timestamp_utc"));
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

    private static int RequiredPositiveInt(JsonElement row, string name)
    {
        var value = ReadInt(row, name)
                    ?? throw new InvalidDataException($"Required parser field '{name}' is missing.");
        if (value < 1)
            throw new InvalidDataException($"Parser field '{name}' must be positive.");
        return value;
    }

    private static int RequiredNonNegativeInt(JsonElement row, string name)
    {
        var value = ReadInt(row, name)
                    ?? throw new InvalidDataException($"Required parser field '{name}' is missing.");
        if (value < 0)
            throw new InvalidDataException($"Parser field '{name}' must be non-negative.");
        return value;
    }

    private static int? ReadNonNegativeInt(JsonElement row, string name)
    {
        var value = ReadInt(row, name);
        if (value is < 0)
            throw new InvalidDataException($"Parser field '{name}' must be non-negative.");
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

    private static string? ScopeString(JsonDocument? document, string name)
    {
        if (document?.RootElement.ValueKind != JsonValueKind.Object)
            return null;

        return ReadString(document.RootElement, name);
    }

    private static bool ScopeBool(JsonDocument? document, string name)
    {
        if (document?.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(name, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false
        };
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
}
