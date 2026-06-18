using System.Text.Json;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;
using AshmesMarketplaces.Application.MarketRecommendations.Services;
using AshmesMarketplaces.Application.ParserObservability.Services;
using AshmesMarketplaces.Application.WorkspaceOverview.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.WorkspaceOverview.Services;

public sealed class WorkspaceOverviewService : IWorkspaceOverviewService
{
    private const string MarketplaceWildberries = "wildberries";
    private const string AlgorithmFallback = "workspace_product_analysis_v1";
    private const string AlgorithmVersionFallback = "0.1.0";
    private const string ModelVersionFallback = "rules";
    private const int MaxCandidatesPerProduct = 80;
    private const int MaxSimilarProducts = 12;
    private const long WbWarehouseDtypeFlag = 8;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> LogisticsSignalCodes = new(StringComparer.Ordinal)
    {
        "seller_stock_slow_central_delivery",
        "top_low_stock_slow_central_delivery",
        "top_slow_cluster_region_delivery",
        "top_slow_central_delivery",
        "peers_slow_region_delivery",
        "faster_than_peers_region_delivery"
    };
    private static readonly HashSet<string> LogisticsSimilarGroupKeys = new(StringComparer.Ordinal)
    {
        "similar_faster_region_delivery",
        "peers_slow_region_delivery",
        "faster_than_peers_region_delivery",
        "top_slow_central_delivery",
        "top_slow_cluster_region_delivery"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IIntelligenceClient _intelligenceClient;

    public WorkspaceOverviewService(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IIntelligenceClient intelligenceClient)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _intelligenceClient = intelligenceClient;
    }

    public async Task<ServiceResult<WorkspaceOverviewResponse>> GetAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var access = await EnsureWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!access.IsSuccess)
            return PropagateError<WorkspaceOverviewResponse>(access.Error!);

        var products = await _dbContext.WorkspaceMarketProducts
            .AsNoTracking()
            .Where(x => x.IdWorkspace == workspaceId)
            .OrderByDescending(x => x.DateUpdate)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var run = await LoadLatestRunAsync(workspaceId, cancellationToken);
        var analyses = run is null
            ? new Dictionary<Guid, WorkspaceMarketProductAnalysis>()
            : await _dbContext.WorkspaceMarketProductAnalyses
                .AsNoTracking()
                .Where(x => x.IdAnalysisRun == run.Id)
                .ToDictionaryAsync(x => x.IdWorkspaceMarketProduct, cancellationToken);

        var productIds = products.Select(x => x.Id).ToHashSet();
        var readStates = await _dbContext.WorkspaceMarketProductUserReadStates
            .AsNoTracking()
            .Where(x => x.IdUser == _currentUser.UserId!.Value && productIds.Contains(x.IdWorkspaceMarketProduct))
            .ToDictionaryAsync(x => x.IdWorkspaceMarketProduct, cancellationToken);
        var currentSnapshots = new Dictionary<Guid, CurrentSnapshot>();
        foreach (var product in products)
        {
            currentSnapshots[product.Id] = await LoadCurrentSnapshotAsync(product, cancellationToken);
        }

        return ServiceResult<WorkspaceOverviewResponse>.Success(MapOverview(products, run, analyses, readStates, currentSnapshots));
    }

    public async Task<ServiceResult<WorkspaceOverviewRecalculateResponse>> RecalculateAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            return ServiceResult<WorkspaceOverviewRecalculateResponse>.Unauthorized("Authentication is required.");

        return await RecalculateForUserAsync(workspaceId, _currentUser.UserId.Value, cancellationToken);
    }

    public async Task<ServiceResult<WorkspaceOverviewRecalculateResponse>> RecalculateForUserAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var access = await EnsureWorkspaceAccessAsync(workspaceId, userId, cancellationToken);
        if (!access.IsSuccess)
            return PropagateError<WorkspaceOverviewRecalculateResponse>(access.Error!);

        var products = await _dbContext.WorkspaceMarketProducts
            .Where(x => x.IdWorkspace == workspaceId)
            .OrderByDescending(x => x.DateUpdate)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var startedAt = DateTime.UtcNow;
        var analyses = new List<PendingAnalysis>();
        var warnings = new List<string>();
        var algorithm = AlgorithmFallback;
        var algorithmVersion = AlgorithmVersionFallback;
        var modelVersion = ModelVersionFallback;
        var successfulAnalyses = 0;
        var failedAnalyses = 0;
        var previousRun = await LoadLatestRunAsync(workspaceId, cancellationToken);
        var previousAnalyses = previousRun is null
            ? new Dictionary<Guid, WorkspaceMarketProductAnalysis>()
            : await _dbContext.WorkspaceMarketProductAnalyses
                .AsNoTracking()
                .Where(x => x.IdAnalysisRun == previousRun.Id)
                .ToDictionaryAsync(x => x.IdWorkspaceMarketProduct, cancellationToken);

        foreach (var product in products)
        {
            var history = await LoadHistoryAsync(product, cancellationToken);
            var candidates = await LoadSimilarCandidatesAsync(product, products, cancellationToken);
            if (product.SourceType == WorkspaceMarketProduct.DemoSourceType && candidates.Count < 3)
                warnings.Add($"Демо-карточка {product.Name}: недостаточно похожих товаров для ценового коридора.");
            var productDeliveryProfiles = await LoadDeliveryProfilesByWbProductIdAsync(
                string.IsNullOrWhiteSpace(product.WbProductId) ? [] : [product.WbProductId],
                cancellationToken);
            var productDeliveryProfile = string.IsNullOrWhiteSpace(product.WbProductId)
                ? null
                : productDeliveryProfiles.GetValueOrDefault(product.WbProductId);
            var requestId = $"workspace-overview-{workspaceId:N}-{product.Id:N}-{startedAt:yyyyMMddHHmmss}";
            var intelligence = await _intelligenceClient.AnalyzeWorkspaceProductAsync(
                new WorkspaceProductAnalysisIntelligenceRequest(
                    requestId,
                    startedAt,
                    MarketplaceWildberries,
                    BuildFeature(product, history, productDeliveryProfile),
                    BuildIntelligenceHistory(history),
                    candidates.Select(x => x.Feature).ToList(),
                    new WorkspaceProductAnalysisOptions(MaxSimilarProducts, AlgorithmFallback)),
                cancellationToken);

            var signals = Array.Empty<WorkspaceOverviewSignalDto>();
            var similarProducts = Array.Empty<WorkspaceOverviewSimilarProductDto>();
            var similarProductGroups = Array.Empty<WorkspaceOverviewSimilarProductGroupDto>();

            if (intelligence.IsSuccess && intelligence.Value is not null)
            {
                algorithm = intelligence.Value.Algorithm;
                algorithmVersion = intelligence.Value.AlgorithmVersion;
                modelVersion = intelligence.Value.ModelVersion;
                warnings.AddRange(intelligence.Value.Warnings);
                signals = intelligence.Value.Signals
                    .Select(x => new WorkspaceOverviewSignalDto(
                        x.Code,
                        x.Severity,
                        x.Title,
                        x.Description,
                        x.MetricFacts,
                        x.Confidence,
                        x.Value))
                    .ToArray();
                similarProducts = MapSimilarProducts(intelligence.Value.SimilarProducts ?? [], candidates);
                similarProductGroups = MapSimilarProductGroups(intelligence.Value.SimilarProductGroups ?? [], candidates);
                if (similarProductGroups.Length == 0 && similarProducts.Length > 0)
                    similarProductGroups = BuildFallbackSimilarProductGroups(product, history, similarProducts);
            }
            else if (intelligence.Error is not null)
            {
                failedAnalyses++;
                warnings.Add($"Товар {product.WbProductId ?? product.Name}: {intelligence.Error.Message}");
                if (previousAnalyses.TryGetValue(product.Id, out var previousAnalysis))
                {
                    analyses.Add(ClonePendingAnalysis(product.Id, previousAnalysis, DateTime.UtcNow));
                    continue;
                }
            }
            else
            {
                failedAnalyses++;
            }

            if (intelligence.IsSuccess)
                successfulAnalyses++;

            analyses.Add(new PendingAnalysis(
                product.Id,
                history.Price.Current,
                history.Price.Previous,
                history.Price.Delta,
                ToInt(history.Position.Current),
                ToInt(history.Position.Previous),
                ToInt(history.Position.Delta),
                ToInt(history.Stock.Current),
                ToInt(history.Stock.Previous),
                ToInt(history.Stock.Delta),
                ToInt(history.Feedback.Current),
                ToInt(history.Feedback.Previous),
                ToInt(history.Feedback.Delta),
                history.ReviewRating.Current,
                history.ReviewRating.Previous,
                history.ReviewRating.Delta,
                history.LatestObservedAtUtc,
                ToJsonDocument(signals),
                ToJsonDocument(similarProducts),
                ToJsonDocument(similarProductGroups),
                DateTime.UtcNow));
        }

        if (products.Count > 0 && successfulAnalyses == 0 && failedAnalyses >= products.Count)
        {
            return ServiceResult<WorkspaceOverviewRecalculateResponse>.Unavailable(
                $"Не удалось пересчитать кластерный анализ: все {products.Count} товаров получили ошибку Intelligence. Последний успешный анализ сохранен.");
        }

        var completedAt = DateTime.UtcNow;
        var run = new WorkspaceMarketProductAnalysisRun(
            workspaceId,
            WorkspaceMarketProductAnalysisRun.CompletedStatus,
            startedAt,
            completedAt,
            algorithm,
            algorithmVersion,
            modelVersion,
            products.Count,
            analyses.Sum(x => DeserializeSignals(x.Signals).Count),
            analyses.Sum(x => DeserializeSimilarProducts(x.SimilarProducts).Count),
            ToJsonDocument(warnings.Distinct(StringComparer.Ordinal).ToArray()),
            null);
        _dbContext.WorkspaceMarketProductAnalysisRuns.Add(run);

        foreach (var analysis in analyses)
        {
            _dbContext.WorkspaceMarketProductAnalyses.Add(new WorkspaceMarketProductAnalysis(
                run.Id,
                analysis.IdWorkspaceMarketProduct,
                analysis.CurrentPrice,
                analysis.PreviousPrice,
                analysis.PriceDelta,
                analysis.CurrentPosition,
                analysis.PreviousPosition,
                analysis.PositionDelta,
                analysis.CurrentStock,
                analysis.PreviousStock,
                analysis.StockDelta,
                analysis.CurrentFeedbackCount,
                analysis.PreviousFeedbackCount,
                analysis.FeedbackDelta,
                analysis.CurrentReviewRating,
                analysis.PreviousReviewRating,
                analysis.ReviewRatingDelta,
                analysis.LatestObservedAtUtc,
                CloneJson(analysis.Signals),
                CloneJson(analysis.SimilarProducts),
                CloneJson(analysis.SimilarProductGroups),
                analysis.ComputedAtUtc));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var runDto = MapRun(run);
        return ServiceResult<WorkspaceOverviewRecalculateResponse>.Success(
            new WorkspaceOverviewRecalculateResponse(
                runDto,
                run.ProductCount,
                run.SignalCount,
                run.SimilarProductCount,
                runDto.Warnings));
    }

    public async Task<ServiceResult<WorkspaceOverviewMarkViewedResponse>> MarkViewedAsync(
        Guid workspaceId,
        WorkspaceOverviewMarkViewedRequest request,
        CancellationToken cancellationToken)
    {
        var access = await EnsureWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!access.IsSuccess)
            return PropagateError<WorkspaceOverviewMarkViewedResponse>(access.Error!);

        if (request.ProductIds.Count == 0)
            return ServiceResult<WorkspaceOverviewMarkViewedResponse>.BadRequest("At least one product id is required.");

        var requestedProductIds = request.ProductIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (requestedProductIds.Length == 0)
            return ServiceResult<WorkspaceOverviewMarkViewedResponse>.BadRequest("At least one valid product id is required.");

        var products = await _dbContext.WorkspaceMarketProducts
            .Where(x => x.IdWorkspace == workspaceId && requestedProductIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (products.Count != requestedProductIds.Length)
            return ServiceResult<WorkspaceOverviewMarkViewedResponse>.NotFound("One or more workspace market products were not found.");

        var userId = _currentUser.UserId!.Value;
        var existingStates = await _dbContext.WorkspaceMarketProductUserReadStates
            .Where(x => x.IdUser == userId && requestedProductIds.Contains(x.IdWorkspaceMarketProduct))
            .ToDictionaryAsync(x => x.IdWorkspaceMarketProduct, cancellationToken);
        var latestRun = await LoadLatestRunAsync(workspaceId, cancellationToken);
        var analyses = latestRun is null
            ? new Dictionary<Guid, WorkspaceMarketProductAnalysis>()
            : await _dbContext.WorkspaceMarketProductAnalyses
                .AsNoTracking()
                .Where(x => x.IdAnalysisRun == latestRun.Id && requestedProductIds.Contains(x.IdWorkspaceMarketProduct))
                .ToDictionaryAsync(x => x.IdWorkspaceMarketProduct, cancellationToken);
        var viewedAt = DateTime.UtcNow;

        foreach (var product in products)
        {
            var snapshot = await LoadCurrentSnapshotAsync(product, cancellationToken);
            analyses.TryGetValue(product.Id, out var analysis);
            var logisticsFactors = BuildLogisticsFactorSnapshotJson(analysis);
            if (existingStates.TryGetValue(product.Id, out var state))
            {
                state.Update(
                    viewedAt,
                    snapshot.ObservedAtUtc,
                    snapshot.Price,
                    snapshot.Position,
                    snapshot.Stock,
                    snapshot.FeedbackCount,
                    snapshot.ReviewRating,
                    logisticsFactors);
            }
            else
            {
                _dbContext.WorkspaceMarketProductUserReadStates.Add(new WorkspaceMarketProductUserReadState(
                    product.Id,
                    userId,
                    viewedAt,
                    snapshot.ObservedAtUtc,
                    snapshot.Price,
                    snapshot.Position,
                    snapshot.Stock,
                    snapshot.FeedbackCount,
                    snapshot.ReviewRating,
                    logisticsFactors));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<WorkspaceOverviewMarkViewedResponse>.Success(
            new WorkspaceOverviewMarkViewedResponse(products.Count, viewedAt));
    }

    private async Task<WorkspaceMarketProductAnalysisRun?> LoadLatestRunAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var runs = await _dbContext.WorkspaceMarketProductAnalysisRuns
            .AsNoTracking()
            .Where(x => x.IdWorkspace == workspaceId)
            .OrderByDescending(x => x.CompletedAtUtc ?? x.StartedAtUtc)
            .ThenByDescending(x => x.StartedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        return runs.FirstOrDefault(x => !IsDefectiveValidationRun(
            x.ProductCount,
            x.SignalCount,
            x.SimilarProductCount,
            DeserializeWarnings(x.Warnings)));
    }

    private WorkspaceOverviewResponse MapOverview(
        IReadOnlyList<WorkspaceMarketProduct> products,
        WorkspaceMarketProductAnalysisRun? run,
        IReadOnlyDictionary<Guid, WorkspaceMarketProductAnalysis> analyses,
        IReadOnlyDictionary<Guid, WorkspaceMarketProductUserReadState> readStates,
        IReadOnlyDictionary<Guid, CurrentSnapshot> currentSnapshots)
    {
        var items = products.Select(product =>
        {
            analyses.TryGetValue(product.Id, out var analysis);
            return MapProduct(product, analysis);
        }).ToList();
        var newItems = products
            .Select(product =>
            {
                analyses.TryGetValue(product.Id, out var analysis);
                readStates.TryGetValue(product.Id, out var readState);
                currentSnapshots.TryGetValue(product.Id, out var currentSnapshot);
                currentSnapshot ??= CurrentSnapshot.FromWorkspaceProduct(product);
                var viewedChanges = BuildViewedChanges(product, currentSnapshot, readState);
                return HasVisibleChange(viewedChanges) || HasLogisticsFactorChange(analysis, readState)
                    ? MapProduct(product, analysis, viewedChanges, currentSnapshot)
                    : null;
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
        var signalCount = items.Sum(x => x.Signals.Count);
        var similarCount = items.Sum(x => x.SimilarProducts.Count);

        return new WorkspaceOverviewResponse(
            run is null ? null : MapRun(run),
            products.Count,
            signalCount,
            similarCount,
            new WorkspaceOverviewGroupDto(
                "new",
                "Новое",
                newItems.Count,
                newItems),
            new WorkspaceOverviewGroupDto(
                WorkspaceMarketProduct.CompetitorTag,
                "Конкуренты",
                items.Count(x => x.TagKey == WorkspaceMarketProduct.CompetitorTag),
                items.Where(x => x.TagKey == WorkspaceMarketProduct.CompetitorTag).ToList()),
            new WorkspaceOverviewGroupDto(
                WorkspaceMarketProduct.IdeaTag,
                "Идеи",
                items.Count(x => x.TagKey == WorkspaceMarketProduct.IdeaTag),
                items.Where(x => x.TagKey == WorkspaceMarketProduct.IdeaTag).ToList()),
            new WorkspaceOverviewGroupDto(
                WorkspaceMarketProduct.CreatedTag,
                "Созданные",
                items.Count(x => x.TagKey == WorkspaceMarketProduct.CreatedTag),
                items.Where(x => x.TagKey == WorkspaceMarketProduct.CreatedTag).ToList()));
    }

    private WorkspaceOverviewProductDto MapProduct(
        WorkspaceMarketProduct product,
        WorkspaceMarketProductAnalysis? analysis,
        ViewedChanges? viewedChanges = null,
        CurrentSnapshot? currentSnapshot = null)
    {
        var signals = analysis is null ? [] : DeserializeSignals(analysis.Signals);
        var similar = analysis is null ? [] : DeserializeSimilarProducts(analysis.SimilarProducts);
        var similarGroups = analysis is null ? [] : DeserializeSimilarProductGroups(analysis.SimilarProductGroups);

        return new WorkspaceOverviewProductDto(
            product.Id,
            product.ParserProductRowId,
            product.WbProductId,
            product.WbRootId,
            product.SourceType,
            product.SourceType == WorkspaceMarketProduct.DemoSourceType,
            product.TagKey,
            product.Note,
            product.Name,
            product.BrandName,
            product.SellerName,
            product.ThumbnailUrl,
            product.SourceCategory,
            product.SourceSubcategory,
            viewedChanges?.Price.CurrentValue ?? currentSnapshot?.Price ?? analysis?.CurrentPrice ?? CurrentPrice(product),
            product.CostPrice,
            product.Description,
            product.SupplierName,
            product.SupplierUrl,
            ToInt(viewedChanges?.Position.CurrentValue) ?? currentSnapshot?.Position ?? analysis?.CurrentPosition ?? product.PositionAbsolute,
            ToInt(viewedChanges?.Stock.CurrentValue) ?? currentSnapshot?.Stock ?? analysis?.CurrentStock ?? product.TotalQuantity,
            ToInt(viewedChanges?.Feedback.CurrentValue) ?? currentSnapshot?.FeedbackCount ?? analysis?.CurrentFeedbackCount ?? product.FeedbackCount,
            viewedChanges?.ReviewRating.CurrentValue ?? currentSnapshot?.ReviewRating ?? analysis?.CurrentReviewRating ?? product.ReviewRating,
            currentSnapshot?.ObservedAtUtc ?? analysis?.LatestObservedAtUtc,
            viewedChanges?.Price ?? Change("price", "Цена", analysis?.CurrentPrice, analysis?.PreviousPrice, analysis?.PriceDelta, "₽"),
            viewedChanges?.Position ?? Change("position", "Позиция", analysis?.CurrentPosition, analysis?.PreviousPosition, analysis?.PositionDelta, null, lowerIsBetter: true),
            viewedChanges?.Stock ?? Change("stock", "Остатки", analysis?.CurrentStock, analysis?.PreviousStock, analysis?.StockDelta, null),
            viewedChanges?.Feedback ?? Change("feedbacks", "Отзывы", analysis?.CurrentFeedbackCount, analysis?.PreviousFeedbackCount, analysis?.FeedbackDelta, null),
            viewedChanges?.ReviewRating ?? Change("reviewRating", "Оценка", analysis?.CurrentReviewRating, analysis?.PreviousReviewRating, analysis?.ReviewRatingDelta, null),
            signals,
            similar,
            similarGroups);
    }

    private async Task<CurrentSnapshot> LoadCurrentSnapshotAsync(
        WorkspaceMarketProduct product,
        CancellationToken cancellationToken)
    {
        var history = await LoadHistoryAsync(product, cancellationToken);

        return new CurrentSnapshot(
            history.Price.Current ?? CurrentPrice(product),
            ToInt(history.Position.Current) ?? product.PositionAbsolute,
            ToInt(history.Stock.Current) ?? product.TotalQuantity,
            ToInt(history.Feedback.Current) ?? product.FeedbackCount,
            history.ReviewRating.Current ?? product.ReviewRating,
            history.LatestObservedAtUtc ?? product.DateUpdate);
    }

    private static ViewedChanges BuildViewedChanges(
        WorkspaceMarketProduct product,
        CurrentSnapshot current,
        WorkspaceMarketProductUserReadState? readState)
    {
        var baseline = readState is null
            ? CurrentSnapshot.FromWorkspaceProduct(product)
            : new CurrentSnapshot(
                readState.BaselinePrice,
                readState.BaselinePosition,
                readState.BaselineStock,
                readState.BaselineFeedbackCount,
                readState.BaselineReviewRating,
                readState.BaselineObservedAtUtc);

        return new ViewedChanges(
            Change("price", "Цена", current.Price, baseline.Price, Delta(current.Price, baseline.Price), "₽"),
            Change("position", "Позиция", current.Position, baseline.Position, Delta(current.Position, baseline.Position), null, lowerIsBetter: true),
            Change("stock", "Остатки", current.Stock, baseline.Stock, Delta(current.Stock, baseline.Stock), null),
            Change("feedbacks", "Отзывы", current.FeedbackCount, baseline.FeedbackCount, Delta(current.FeedbackCount, baseline.FeedbackCount), null),
            Change("reviewRating", "Оценка", current.ReviewRating, baseline.ReviewRating, Delta(current.ReviewRating, baseline.ReviewRating), null));
    }

    private static bool HasVisibleChange(ViewedChanges changes) =>
        HasChange(changes.Price)
        || HasChange(changes.Position)
        || HasChange(changes.Stock)
        || HasChange(changes.Feedback)
        || HasChange(changes.ReviewRating);

    private static bool HasLogisticsFactorChange(
        WorkspaceMarketProductAnalysis? analysis,
        WorkspaceMarketProductUserReadState? readState)
    {
        var current = BuildLogisticsFactorSnapshotEntries(analysis);
        if (current.Count == 0)
            return false;

        var currentJson = JsonSerializer.Serialize(current, JsonOptions);
        var baselineJson = readState?.BaselineLogisticsFactors?.RootElement.GetRawText();
        return !string.Equals(currentJson, baselineJson, StringComparison.Ordinal);
    }

    private static JsonDocument BuildLogisticsFactorSnapshotJson(WorkspaceMarketProductAnalysis? analysis) =>
        ToJsonDocument(BuildLogisticsFactorSnapshotEntries(analysis));

    private static IReadOnlyList<string> BuildLogisticsFactorSnapshotEntries(WorkspaceMarketProductAnalysis? analysis)
    {
        if (analysis is null)
            return [];

        var entries = new List<string>();
        foreach (var signal in DeserializeSignals(analysis.Signals))
        {
            if (!LogisticsSignalCodes.Contains(signal.Code))
                continue;

            var facts = string.Join("|", signal.MetricFacts.OrderBy(x => x, StringComparer.Ordinal));
            entries.Add($"signal:{signal.Code}:{signal.Title}:{facts}");
        }

        foreach (var group in DeserializeSimilarProductGroups(analysis.SimilarProductGroups))
        {
            if (!LogisticsSimilarGroupKeys.Contains(group.Key))
                continue;

            foreach (var item in group.Items)
            {
                var facts = string.Join(
                    "|",
                    item.Facts.Concat(item.Tags).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));
                entries.Add($"group:{group.Key}:{item.Product.ProductKey}:{facts}");
            }
        }

        return entries
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasChange(WorkspaceOverviewChangeDto change) =>
        change.Delta.HasValue && change.Delta.Value != 0;

    private static decimal? Delta(decimal? current, decimal? previous) =>
        current.HasValue && previous.HasValue ? current - previous : null;

    private static int? Delta(int? current, int? previous) =>
        current.HasValue && previous.HasValue ? current - previous : null;

    private static WorkspaceOverviewChangeDto Change(
        string key,
        string label,
        decimal? current,
        decimal? previous,
        decimal? delta,
        string? suffix,
        bool lowerIsBetter = false)
    {
        if (current is null || previous is null || delta is null)
            return new WorkspaceOverviewChangeDto(key, label, current, previous, delta, "Недостаточно наблюдений", "unknown");

        var prefix = delta > 0 ? "+" : string.Empty;
        var value = suffix is null
            ? $"{prefix}{FormatDecimal(delta.Value)}"
            : $"{prefix}{FormatDecimal(delta.Value)} {suffix}";
        var state = delta == 0
            ? "neutral"
            : lowerIsBetter
                ? delta < 0 ? "positive" : "negative"
                : delta > 0 ? "positive" : "negative";
        return new WorkspaceOverviewChangeDto(key, label, current, previous, delta, value, state);
    }

    private static WorkspaceOverviewChangeDto Change(
        string key,
        string label,
        int? current,
        int? previous,
        int? delta,
        string? suffix,
        bool lowerIsBetter = false) =>
        Change(key, label, ToDecimal(current), ToDecimal(previous), ToDecimal(delta), suffix, lowerIsBetter);

    private static WorkspaceOverviewRunDto MapRun(WorkspaceMarketProductAnalysisRun run) =>
        new(
            run.Id,
            run.Status,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.Algorithm,
            run.AlgorithmVersion,
            run.ModelVersion,
            DeserializeWarnings(run.Warnings),
            run.ErrorMessage);

    private static PendingAnalysis ClonePendingAnalysis(
        Guid idWorkspaceMarketProduct,
        WorkspaceMarketProductAnalysis analysis,
        DateTime computedAtUtc) =>
        new(
            idWorkspaceMarketProduct,
            analysis.CurrentPrice,
            analysis.PreviousPrice,
            analysis.PriceDelta,
            analysis.CurrentPosition,
            analysis.PreviousPosition,
            analysis.PositionDelta,
            analysis.CurrentStock,
            analysis.PreviousStock,
            analysis.StockDelta,
            analysis.CurrentFeedbackCount,
            analysis.PreviousFeedbackCount,
            analysis.FeedbackDelta,
            analysis.CurrentReviewRating,
            analysis.PreviousReviewRating,
            analysis.ReviewRatingDelta,
            analysis.LatestObservedAtUtc,
            CloneJson(analysis.Signals),
            CloneJson(analysis.SimilarProducts),
            CloneJson(analysis.SimilarProductGroups),
            computedAtUtc);

    public static bool IsDefectiveValidationRunForTesting(
        int productCount,
        int signalCount,
        int similarProductCount,
        IReadOnlyList<string> warnings) =>
        IsDefectiveValidationRun(productCount, signalCount, similarProductCount, warnings);

    private static bool IsDefectiveValidationRun(
        int productCount,
        int signalCount,
        int similarProductCount,
        IReadOnlyList<string> warnings)
    {
        if (productCount <= 0 || signalCount != 0 || similarProductCount != 0 || warnings.Count < productCount)
            return false;

        var validationWarnings = warnings.Count(IsWorkspaceAnalysisValidationWarning);
        return validationWarnings >= productCount && validationWarnings == warnings.Count;
    }

    private static bool IsWorkspaceAnalysisValidationWarning(string warning) =>
        warning.Contains("Request payload validation failed:", StringComparison.OrdinalIgnoreCase)
        && warning.Contains("Extra inputs are not permitted", StringComparison.OrdinalIgnoreCase);

    private async Task<ProductHistory> LoadHistoryAsync(WorkspaceMarketProduct product, CancellationToken cancellationToken)
    {
        if (product.SourceType == WorkspaceMarketProduct.DemoSourceType || string.IsNullOrWhiteSpace(product.WbProductId))
        {
            var price = CurrentPrice(product);
            var demoPriceRows = price.HasValue
                ? new List<HistoryPoint> { new(product.DateUpdate, price.Value) }
                : [];
            return new ProductHistory(
                ToChange(demoPriceRows),
                ToChange([]),
                ToChange([]),
                ToChange([]),
                ToChange([]),
                product.DateUpdate,
                demoPriceRows,
                [],
                [],
                [],
                []);
        }

        var priceRows = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => x.WbProductId == product.WbProductId
                && x.SourceSubcategory == product.SourceSubcategory
                && x.SourceRegionDest == product.SourceRegionDest)
            .OrderByDescending(x => x.ParsedAtUtc)
            .Take(8)
            .Select(x => new HistoryPoint(x.ParsedAtUtc, x.PriceWbWallet ?? x.PriceDiscounted ?? x.PriceRegular))
            .ToListAsync(cancellationToken);

        var feedbackRows = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => x.WbProductId == product.WbProductId
                && x.SourceSubcategory == product.SourceSubcategory
                && x.SourceRegionDest == product.SourceRegionDest)
            .OrderByDescending(x => x.ParsedAtUtc)
            .Take(8)
            .Select(x => new HistoryPoint(x.ParsedAtUtc, x.FeedbackCount))
            .ToListAsync(cancellationToken);

        var reviewRatingRows = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => x.WbProductId == product.WbProductId
                && x.SourceSubcategory == product.SourceSubcategory
                && x.SourceRegionDest == product.SourceRegionDest)
            .OrderByDescending(x => x.ParsedAtUtc)
            .Take(8)
            .Select(x => new HistoryPoint(x.ParsedAtUtc, x.ReviewRating ?? x.RatingRounded))
            .ToListAsync(cancellationToken);

        var positionRows = await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x => x.WbProductId == product.WbProductId
                && x.SourceSubcategory == product.SourceSubcategory
                && x.SourceRegionDest == product.SourceRegionDest)
            .OrderByDescending(x => x.ObservedAtUtc)
            .Take(8)
            .Select(x => new HistoryPoint(x.ObservedAtUtc, x.AbsolutePosition))
            .ToListAsync(cancellationToken);

        var stockRows = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x => x.WbProductId == product.WbProductId
                && x.SourceSubcategory == product.SourceSubcategory
                && x.SourceRegionDest == product.SourceRegionDest)
            .OrderByDescending(x => x.ObservedAtUtc)
            .Take(8)
            .Select(x => new HistoryPoint(x.ObservedAtUtc, x.TotalQuantityObserved))
            .ToListAsync(cancellationToken);

        var latestObserved = new[]
            {
                priceRows.FirstOrDefault()?.ObservedAtUtc,
                feedbackRows.FirstOrDefault()?.ObservedAtUtc,
                reviewRatingRows.FirstOrDefault()?.ObservedAtUtc,
                positionRows.FirstOrDefault()?.ObservedAtUtc,
                stockRows.FirstOrDefault()?.ObservedAtUtc
            }
            .Where(x => x.HasValue)
            .Max();

        return new ProductHistory(
            ToChange(priceRows),
            ToChange(positionRows),
            ToChange(stockRows),
            ToChange(feedbackRows),
            ToChange(reviewRatingRows),
            latestObserved,
            priceRows,
            positionRows,
            stockRows,
            feedbackRows,
            reviewRatingRows);
    }

    private async Task<IReadOnlyList<CandidateFeature>> LoadSimilarCandidatesAsync(
        WorkspaceMarketProduct product,
        IReadOnlyList<WorkspaceMarketProduct> workspaceProducts,
        CancellationToken cancellationToken)
    {
        var excluded = workspaceProducts
            .Select(x => x.WbProductId)
            .Append(product.WbProductId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        var sourceSubcategory = product.SourceSubcategory;
        if (string.IsNullOrWhiteSpace(sourceSubcategory))
            return [];

        var query = _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => x.SourceSubcategory == sourceSubcategory
                && !excluded.Contains(x.WbProductId));

        if (product.SourceType != WorkspaceMarketProduct.DemoSourceType && !string.IsNullOrWhiteSpace(product.SourceRegionDest))
            query = query.Where(x => x.SourceRegionDest == product.SourceRegionDest);

        var rows = await query
            .OrderByDescending(x => x.ParsedAtUtc)
            .Take(300)
            .ToListAsync(cancellationToken);

        var latestRows = rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(row => row.ParsedAtUtc).First())
            .Take(MaxCandidatesPerProduct)
            .ToList();

        var candidateIds = latestRows.Select(x => x.WbProductId).ToHashSet(StringComparer.Ordinal);
        var positionRows = await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x => candidateIds.Contains(x.WbProductId)
                && x.SourceSubcategory == product.SourceSubcategory
                && (product.SourceType == WorkspaceMarketProduct.DemoSourceType
                    || product.SourceRegionDest == null
                    || x.SourceRegionDest == product.SourceRegionDest))
            .OrderByDescending(x => x.ObservedAtUtc)
            .Select(x => new
            {
                x.WbProductId,
                x.AbsolutePosition
            })
            .ToListAsync(cancellationToken);
        var positions = positionRows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => (int?)x.First().AbsolutePosition, StringComparer.Ordinal);
        var deliveryProfiles = await LoadDeliveryProfilesByWbProductIdAsync(
            latestRows.Select(x => x.WbProductId).Distinct(StringComparer.Ordinal).ToArray(),
            cancellationToken);
        var reviewQuality = await LoadReviewQualityByWbProductIdAsync(candidateIds, cancellationToken);
        var details = await LoadProductDetailsByWbProductIdAsync(candidateIds, cancellationToken);

        return latestRows
            .Select(row => new CandidateFeature(
                row,
                BuildFeature(
                    row,
                    deliveryProfiles.GetValueOrDefault(row.WbProductId),
                    reviewQuality.GetValueOrDefault(row.WbProductId),
                    details.GetValueOrDefault(row.WbProductId)),
                positions.GetValueOrDefault(row.WbProductId)))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, ProductDetails>> LoadProductDetailsByWbProductIdAsync(
        IReadOnlyCollection<string> wbProductIds,
        CancellationToken cancellationToken)
    {
        var productIds = wbProductIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (productIds.Count == 0)
            return new Dictionary<string, ProductDetails>(StringComparer.Ordinal);

        var rows = await _dbContext.ParserProductDetailRows
            .AsNoTracking()
            .Where(x => productIds.Contains(x.WbProductId))
            .OrderByDescending(x => x.ParsedAtUtc)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var row = x.First();
                    return new ProductDetails(
                        row.Description,
                        row.Characteristics?.RootElement.Clone());
                },
                StringComparer.Ordinal);
    }

    private async Task<IReadOnlyDictionary<string, ReviewQuality>> LoadReviewQualityByWbProductIdAsync(
        IReadOnlyCollection<string> wbProductIds,
        CancellationToken cancellationToken)
    {
        var productIds = wbProductIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (productIds.Count == 0)
            return new Dictionary<string, ReviewQuality>(StringComparer.Ordinal);

        var rows = await _dbContext.ParserReviewRows
            .AsNoTracking()
            .Where(x => productIds.Contains(x.WbProductId))
            .Select(x => new { x.WbProductId, x.Rating })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var rated = x.Where(row => row.Rating.HasValue).ToList();
                    return new ReviewQuality(
                        rated.Count(row => row.Rating >= 4),
                        rated.Count);
                },
                StringComparer.Ordinal);
    }

    private async Task<IReadOnlyDictionary<string, MarketProductDeliveryProfileDto>> LoadDeliveryProfilesByWbProductIdAsync(
        IReadOnlyCollection<string> wbProductIds,
        CancellationToken cancellationToken)
    {
        var productIds = wbProductIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (productIds.Count == 0)
            return new Dictionary<string, MarketProductDeliveryProfileDto>(StringComparer.Ordinal);

        var rows = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x => productIds.Contains(x.WbProductId)
                && x.DeliveryProfileKey != null
                && x.SourceRegionDest != null)
            .Select(x => new DeliveryProfileRow(
                x.WbProductId,
                x.SourceRegionDest,
                x.DeliveryProfileKey,
                x.DeliveryDestinationName,
                x.DeliveryDestinationCity,
                x.DeliveryDestinationAddress,
                x.TotalQuantityObserved,
                x.ProductTime1Raw,
                x.ProductTime2Raw,
                x.ProductDtypeRaw,
                x.VisibleDeliveryLabel,
                x.VisibleDeliveryDate,
                x.ObservedAtUtc,
                x.SourceLineNumber))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .Select(group =>
            {
                var destinations = group
                    .GroupBy(row => row.SourceRegionDest, StringComparer.Ordinal)
                    .Select(destinationGroup => destinationGroup
                        .OrderByDescending(row => row.ObservedAtUtc)
                        .ThenByDescending(row => row.SourceLineNumber)
                        .First())
                    .OrderBy(row => row.DeliveryProfileKey, StringComparer.Ordinal)
                    .ThenBy(row => row.SourceRegionDest, StringComparer.Ordinal)
                    .Select(row =>
                    {
                        var calculated = WbVisibleDeliveryCalculator.Calculate(
                            row.ProductTime1Raw,
                            row.ProductTime2Raw,
                            row.ProductDtypeRaw,
                            row.TotalQuantityObserved,
                            row.ObservedAtUtc);

                        return new MarketProductDeliveryDestinationDto(
                            RegionKeyFrom(row),
                            RegionNameFrom(row),
                            row.DeliveryDestinationCity ?? row.DeliveryDestinationName,
                            row.DeliveryDestinationAddress,
                            calculated.Label ?? row.VisibleDeliveryLabel,
                            calculated.Date ?? row.VisibleDeliveryDate,
                            DeliveryHoursFrom(row),
                            DeliverySourceTypeFrom(row.ProductDtypeRaw),
                            row.TotalQuantityObserved,
                            calculated.ObservedAtUtc ?? row.ObservedAtUtc);
                    })
                    .Where(x => x.DeliveryHours.HasValue || x.VisibleDeliveryDate.HasValue)
                    .ToList();

                return new { group.Key, Destinations = destinations };
            })
            .Where(x => x.Destinations.Count > 0)
            .ToDictionary(
                x => x.Key,
                x => new MarketProductDeliveryProfileDto(x.Destinations),
                StringComparer.Ordinal);
    }

    private static MarketProductFeatureDto BuildFeature(
        WorkspaceMarketProduct product,
        ProductHistory history,
        MarketProductDeliveryProfileDto? deliveryProfile) =>
        new(
            $"workspace:{product.Id:N}",
            product.WbProductId,
            product.WbRootId,
            product.Name,
            product.BrandName,
            product.SellerName,
            product.SourceCategory,
            product.SourceSubcategory,
            history.Price.Current ?? CurrentPrice(product),
            product.PriceRegular,
            product.PriceWbWallet,
            product.ReviewRating,
            product.FeedbackCount,
            null,
            null,
            ToInt(history.Position.Current) ?? product.PositionAbsolute,
            null,
            null,
            ToInt(history.Stock.Current) ?? product.TotalQuantity,
            history.LatestObservedAtUtc,
            Description: product.Description,
            Characteristics: ParseJsonElement(product.CharacteristicsJson),
            ImageCount: product.SourceType == WorkspaceMarketProduct.DemoSourceType && product.ThumbnailUrl is not null ? 1 : null,
            ReviewSignals: null,
            DeliveryProfile: deliveryProfile,
            SourceType: product.SourceType,
            CostPrice: product.CostPrice,
            SupplierName: product.SupplierName,
            SupplierUrl: product.SupplierUrl);

    private static MarketProductFeatureDto BuildFeature(
        ParserProductRow row,
        MarketProductDeliveryProfileDto? deliveryProfile,
        ReviewQuality? reviewQuality = null,
        ProductDetails? details = null) =>
        new(
            $"parser:{row.Id:N}",
            row.WbProductId,
            row.WbRootId,
            row.Name,
            row.BrandName,
            row.SellerName,
            row.SourceCategory,
            row.SourceSubcategory,
            row.PriceDiscounted,
            row.PriceRegular,
            row.PriceWbWallet,
            row.ReviewRating ?? row.RatingRounded,
            row.FeedbackCount,
            null,
            null,
            null,
            null,
            null,
            row.TotalQuantity,
            row.ParsedAtUtc,
            Description: details?.Description,
            Characteristics: details?.Characteristics,
            ImageCount: row.ImageCount,
            ReviewSignals: null,
            DeliveryProfile: deliveryProfile,
            SourceType: WorkspaceMarketProduct.ParserSourceType,
            PositiveReviewCount: reviewQuality?.PositiveReviewCount,
            ReviewSampleSize: reviewQuality?.ReviewSampleSize);

    private static WorkspaceProductHistoryDto BuildIntelligenceHistory(ProductHistory history) =>
        new(
            history.PricePoints.Select(ToIntelligencePoint).ToList(),
            history.PositionPoints.Select(ToIntelligencePoint).ToList(),
            history.StockPoints.Select(ToIntelligencePoint).ToList(),
            history.FeedbackPoints.Select(ToIntelligencePoint).ToList());

    private static WorkspaceProductHistoryPointDto ToIntelligencePoint(HistoryPoint point) =>
        new(point.ObservedAtUtc, point.Value);

    private static ProductChange ToChange(IReadOnlyList<HistoryPoint> points)
    {
        var current = points.Count > 0 ? points[0].Value : null;
        var previous = points.Count > 1 ? points[1].Value : null;
        var delta = current.HasValue && previous.HasValue ? current - previous : null;
        return new ProductChange(current, previous, delta);
    }

    private static WorkspaceOverviewSimilarProductDto[] MapSimilarProducts(
        IReadOnlyList<WorkspaceSimilarProductDto> similar,
        IReadOnlyList<CandidateFeature> candidates)
    {
        var map = candidates.ToDictionary(x => x.Feature.ProductKey, x => x, StringComparer.Ordinal);
        return similar
            .Where(x => map.ContainsKey(x.ProductKey))
            .Take(MaxSimilarProducts)
            .Select(x =>
            {
                var candidate = map[x.ProductKey];
                var row = candidate.Row;
                return new WorkspaceOverviewSimilarProductDto(
                    x.ProductKey,
                    row.Id,
                    row.WbProductId,
                    row.WbRootId,
                    row.Name,
                    row.BrandName,
                    row.SellerName,
                    ExtractFirstImage(row.ImageUrls),
                    row.SourceSubcategory,
                    row.PriceWbWallet ?? row.PriceDiscounted ?? row.PriceRegular,
                    row.ReviewRating ?? row.RatingRounded,
                    row.FeedbackCount,
                    candidate.Position,
                    row.TotalQuantity,
                    x.SimilarityScore,
                    x.Reason);
            })
            .ToArray();
    }

    private static WorkspaceOverviewSimilarProductGroupDto[] MapSimilarProductGroups(
        IReadOnlyList<WorkspaceSimilarProductGroupDto> groups,
        IReadOnlyList<CandidateFeature> candidates)
    {
        var products = MapSimilarProducts(
                groups.SelectMany(x => x.Items)
                    .Select(x => new WorkspaceSimilarProductDto(x.ProductKey, null, null, 0, string.Empty))
                    .GroupBy(x => x.ProductKey, StringComparer.Ordinal)
                    .Select(x => x.First())
                    .ToArray(),
                candidates)
            .ToDictionary(x => x.ProductKey, StringComparer.Ordinal);

        return groups
            .Select(group => new WorkspaceOverviewSimilarProductGroupDto(
                group.Key,
                group.Title,
                group.Description,
                group.Items
                    .Where(item => products.ContainsKey(item.ProductKey))
                    .Select(item => new WorkspaceOverviewSimilarProductGroupItemDto(
                        products[item.ProductKey],
                        item.Facts,
                        item.Tags))
                    .ToArray()))
            .Where(group => group.Items.Count > 0)
            .ToArray();
    }

    private static WorkspaceOverviewSimilarProductGroupDto[] BuildFallbackSimilarProductGroups(
        WorkspaceMarketProduct product,
        ProductHistory history,
        IReadOnlyList<WorkspaceOverviewSimilarProductDto> similarProducts)
    {
        var productPrice = history.Price.Current ?? CurrentPrice(product);
        var productPosition = ToInt(history.Position.Current) ?? product.PositionAbsolute;
        var productFeedback = ToInt(history.Feedback.Current) ?? product.FeedbackCount;
        var productRating = history.ReviewRating.Current ?? product.ReviewRating;
        var productStock = ToInt(history.Stock.Current) ?? product.TotalQuantity;

        var groups = new[]
        {
            BuildGroup(
                "price_disadvantage",
                "Дешевле",
                "Похожие карточки дешевле текущей наблюдаемой карточки.",
                similarProducts
                    .Where(x => productPrice.HasValue && x.Price.HasValue && x.Price.Value < productPrice.Value)
                    .OrderByDescending(x => productPrice!.Value - x.Price!.Value)
                    .Take(MaxSimilarProducts)
                    .ToArray(),
                PriceDisadvantageFacts),
            BuildGroup(
                "position_disadvantage",
                "Выше в выдаче",
                "Похожие карточки стоят выше в выдаче.",
                similarProducts
                    .Where(x => productPosition.HasValue && x.Position.HasValue && x.Position.Value < productPosition.Value)
                    .OrderByDescending(x => productPosition!.Value - x.Position!.Value)
                    .Take(MaxSimilarProducts)
                    .ToArray(),
                PositionDisadvantageFacts),
            BuildGroup(
                "review_count_disadvantage",
                "Больше отзывов",
                "У похожих карточек отзывов больше, чем у текущей.",
                similarProducts
                    .Where(x => productFeedback.HasValue && x.FeedbackCount.HasValue && x.FeedbackCount.Value > productFeedback.Value)
                    .OrderByDescending(x => x.FeedbackCount!.Value - productFeedback!.Value)
                    .Take(MaxSimilarProducts)
                    .ToArray(),
                FeedbackDisadvantageFacts),
            BuildGroup(
                "rating_disadvantage",
                "Оценка выше",
                "У похожих карточек оценка выше, чем у текущей.",
                similarProducts
                    .Where(x => productRating.HasValue && x.Rating.HasValue && x.Rating.Value > productRating.Value)
                    .OrderByDescending(x => x.Rating!.Value - productRating!.Value)
                    .Take(MaxSimilarProducts)
                    .ToArray(),
                RatingDisadvantageFacts),
            BuildGroup(
                "stock_disadvantage",
                "Остаток выше",
                "У похожих карточек наблюдаемый остаток выше.",
                similarProducts
                    .Where(x => productStock.HasValue && x.TotalQuantity.HasValue && x.TotalQuantity.Value > productStock.Value)
                    .OrderByDescending(x => x.TotalQuantity!.Value - productStock!.Value)
                    .Take(MaxSimilarProducts)
                    .ToArray(),
                StockDisadvantageFacts),
            BuildGroup(
                "weak_competitor_cards",
                "Слабые похожие",
                "Похожие карточки с собственными слабыми параметрами.",
                similarProducts.Where(IsWeakCompetitor).Take(MaxSimilarProducts).ToArray(),
                WeakCompetitorFacts)
        };

        return groups.Where(x => x.Items.Count > 0).ToArray();

        bool IsWeakCompetitor(WorkspaceOverviewSimilarProductDto item) =>
            item.Rating is < 4.5m
            || item.FeedbackCount is < 25
            || item.TotalQuantity is <= 5;

        WorkspaceOverviewSimilarProductGroupDto BuildGroup(
            string key,
            string title,
            string description,
            IReadOnlyList<WorkspaceOverviewSimilarProductDto> items,
            Func<WorkspaceOverviewSimilarProductDto, IReadOnlyList<string>> factsFactory) =>
            new(
                key,
                title,
                description,
                items.Select(item =>
                {
                    var facts = factsFactory(item);
                    return new WorkspaceOverviewSimilarProductGroupItemDto(item, facts, facts);
                }).ToArray());

        IReadOnlyList<string> PriceDisadvantageFacts(WorkspaceOverviewSimilarProductDto item) =>
            productPrice.HasValue && item.Price.HasValue
                ? [$"Похожая дешевле на {FormatDecimal(productPrice.Value - item.Price.Value)} ₽"]
                : [];

        IReadOnlyList<string> PositionDisadvantageFacts(WorkspaceOverviewSimilarProductDto item) =>
            productPosition.HasValue && item.Position.HasValue
                ? [$"Похожая выше на {FormatDecimal(productPosition.Value - item.Position.Value)} {PlaceWord(productPosition.Value - item.Position.Value)}"]
                : [];

        IReadOnlyList<string> FeedbackDisadvantageFacts(WorkspaceOverviewSimilarProductDto item) =>
            productFeedback.HasValue && item.FeedbackCount.HasValue
                ? [$"У похожей отзывов больше на {FormatDecimal(item.FeedbackCount.Value - productFeedback.Value)}"]
                : [];

        IReadOnlyList<string> RatingDisadvantageFacts(WorkspaceOverviewSimilarProductDto item) =>
            productRating.HasValue && item.Rating.HasValue
                ? [$"Оценка похожей выше на {FormatDecimal(item.Rating.Value - productRating.Value)}"]
                : [];

        IReadOnlyList<string> StockDisadvantageFacts(WorkspaceOverviewSimilarProductDto item) =>
            productStock.HasValue && item.TotalQuantity.HasValue
                ? [$"У похожей остаток выше на {FormatDecimal(item.TotalQuantity.Value - productStock.Value)}"]
                : [];

        IReadOnlyList<string> WeakCompetitorFacts(WorkspaceOverviewSimilarProductDto item)
        {
            var facts = new List<string>();
            if (item.Rating is < 4.5m)
                facts.Add($"У похожей низкая оценка: {FormatDecimal(item.Rating.Value)}");
            if (item.FeedbackCount is < 25)
                facts.Add($"У похожей мало отзывов: {item.FeedbackCount}");
            if (item.TotalQuantity is <= 5)
                facts.Add($"У похожей низкий остаток: {item.TotalQuantity}");
            return facts;
        }
    }

    private async Task<ServiceResult> EnsureWorkspaceAccessAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            return ServiceResult.Unauthorized("Authentication is required.");

        return await EnsureWorkspaceAccessAsync(workspaceId, _currentUser.UserId.Value, cancellationToken);
    }

    private async Task<ServiceResult> EnsureWorkspaceAccessAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty)
            return ServiceResult.BadRequest("Workspace id is required.");

        if (userId == Guid.Empty)
            return ServiceResult.Unauthorized("Authentication is required.");

        var exists = await _dbContext.Workspaces.AsNoTracking().AnyAsync(x => x.Id == workspaceId, cancellationToken);
        if (!exists)
            return ServiceResult.NotFound("Workspace was not found.");

        var hasAccess = await _dbContext.UserWorkspaces
            .AsNoTracking()
            .AnyAsync(x => x.IdWorkspace == workspaceId && x.IdUser == userId, cancellationToken);

        return hasAccess
            ? ServiceResult.Success()
            : ServiceResult.Forbidden("User does not have access to this workspace.");
    }

    private static ServiceResult<T> PropagateError<T>(ServiceError error) =>
        error.Type switch
        {
            ServiceErrorType.BadRequest => ServiceResult<T>.BadRequest(error.Message),
            ServiceErrorType.NotFound => ServiceResult<T>.NotFound(error.Message),
            ServiceErrorType.Unauthorized => ServiceResult<T>.Unauthorized(error.Message),
            ServiceErrorType.Unavailable => ServiceResult<T>.Unavailable(error.Message),
            _ => ServiceResult<T>.Forbidden(error.Message)
        };

    private static decimal? CurrentPrice(WorkspaceMarketProduct product) =>
        product.PriceWbWallet ?? product.PriceDiscounted ?? product.PriceRegular;

    private static int? ToInt(decimal? value) => value.HasValue ? decimal.ToInt32(value.Value) : null;

    private static decimal? ToDecimal(int? value) => value;

    private static string FormatDecimal(decimal value) =>
        value % 1 == 0 ? decimal.ToInt32(value).ToString("N0", new System.Globalization.CultureInfo("ru-RU")) : value.ToString("N2", new System.Globalization.CultureInfo("ru-RU"));

    private static string PlaceWord(int value)
    {
        var normalized = Math.Abs(value);
        var lastTwo = normalized % 100;
        var last = normalized % 10;

        if (lastTwo is >= 11 and <= 14)
            return "мест";

        return last switch
        {
            1 => "место",
            >= 2 and <= 4 => "места",
            _ => "мест"
        };
    }

    private static string RegionKeyFrom(DeliveryProfileRow row)
    {
        var value = row.DeliveryDestinationName ?? row.DeliveryDestinationCity ?? row.SourceRegionDest;
        return value.Trim().ToLowerInvariant() switch
        {
            var x when x.Contains("моск", StringComparison.OrdinalIgnoreCase) => "central",
            var x when x.Contains("санкт", StringComparison.OrdinalIgnoreCase) || x.Contains("петербург", StringComparison.OrdinalIgnoreCase) => "northwest",
            var x when x.Contains("казан", StringComparison.OrdinalIgnoreCase) => "volga",
            var x when x.Contains("екатеринбург", StringComparison.OrdinalIgnoreCase) => "ural",
            var x when x.Contains("новосибирск", StringComparison.OrdinalIgnoreCase) => "siberia",
            var x when x.Contains("краснодар", StringComparison.OrdinalIgnoreCase) => "south",
            var x when x.Contains("хабаровск", StringComparison.OrdinalIgnoreCase) || x.Contains("владивосток", StringComparison.OrdinalIgnoreCase) => "far_east",
            _ => row.SourceRegionDest
        };
    }

    private static string RegionNameFrom(DeliveryProfileRow row) =>
        RegionKeyFrom(row) switch
        {
            "central" => "Центральный регион",
            "northwest" => "Северо-Запад",
            "volga" => "Поволжье",
            "ural" => "Урал",
            "siberia" => "Сибирь",
            "south" => "Юг",
            "far_east" => "Дальний Восток",
            _ => row.DeliveryDestinationName ?? row.DeliveryDestinationCity ?? row.SourceRegionDest
        };

    private static int? DeliveryHoursFrom(DeliveryProfileRow row) =>
        row.ProductTime1Raw.HasValue && row.ProductTime2Raw.HasValue
            ? row.ProductTime1Raw.Value + row.ProductTime2Raw.Value
            : null;

    private static string DeliverySourceTypeFrom(long? dtype)
    {
        if (!dtype.HasValue)
            return "unknown";

        return (dtype.Value & WbWarehouseDtypeFlag) != 0
            ? "wb_warehouse"
            : "seller_warehouse";
    }

    private static JsonDocument ToJsonDocument<T>(T value) =>
        JsonSerializer.SerializeToDocument(value, JsonOptions);

    private static JsonDocument CloneJson(JsonDocument document) =>
        JsonDocument.Parse(document.RootElement.GetRawText());

    private static JsonElement? ParseJsonElement(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> DeserializeWarnings(JsonDocument document) =>
        JsonSerializer.Deserialize<IReadOnlyList<string>>(document.RootElement.GetRawText(), JsonOptions) ?? [];

    private static IReadOnlyList<WorkspaceOverviewSignalDto> DeserializeSignals(JsonDocument document) =>
        JsonSerializer.Deserialize<IReadOnlyList<WorkspaceOverviewSignalDto>>(document.RootElement.GetRawText(), JsonOptions) ?? [];

    private static IReadOnlyList<WorkspaceOverviewSimilarProductDto> DeserializeSimilarProducts(JsonDocument document) =>
        JsonSerializer.Deserialize<IReadOnlyList<WorkspaceOverviewSimilarProductDto>>(document.RootElement.GetRawText(), JsonOptions) ?? [];

    private static IReadOnlyList<WorkspaceOverviewSimilarProductGroupDto> DeserializeSimilarProductGroups(JsonDocument document) =>
        JsonSerializer.Deserialize<IReadOnlyList<WorkspaceOverviewSimilarProductGroupDto>>(document.RootElement.GetRawText(), JsonOptions) ?? [];

    private static string? ExtractFirstImage(JsonDocument? imageUrls)
    {
        if (imageUrls is null || imageUrls.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in imageUrls.RootElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return null;
    }

    private sealed record ProductHistory(
        ProductChange Price,
        ProductChange Position,
        ProductChange Stock,
        ProductChange Feedback,
        ProductChange ReviewRating,
        DateTime? LatestObservedAtUtc,
        IReadOnlyList<HistoryPoint> PricePoints,
        IReadOnlyList<HistoryPoint> PositionPoints,
        IReadOnlyList<HistoryPoint> StockPoints,
        IReadOnlyList<HistoryPoint> FeedbackPoints,
        IReadOnlyList<HistoryPoint> ReviewRatingPoints);

    private sealed record ProductChange(decimal? Current, decimal? Previous, decimal? Delta);

    private sealed record HistoryPoint(DateTime ObservedAtUtc, decimal? Value);

    private sealed record CurrentSnapshot(
        decimal? Price,
        int? Position,
        int? Stock,
        int? FeedbackCount,
        decimal? ReviewRating,
        DateTime? ObservedAtUtc)
    {
        public static CurrentSnapshot FromWorkspaceProduct(WorkspaceMarketProduct product) =>
            new(
                CurrentPrice(product),
                product.PositionAbsolute,
                product.TotalQuantity,
                product.FeedbackCount,
                product.ReviewRating,
                product.DateUpdate);
    }

    private sealed record ViewedChanges(
        WorkspaceOverviewChangeDto Price,
        WorkspaceOverviewChangeDto Position,
        WorkspaceOverviewChangeDto Stock,
        WorkspaceOverviewChangeDto Feedback,
        WorkspaceOverviewChangeDto ReviewRating);

    private sealed record CandidateFeature(ParserProductRow Row, MarketProductFeatureDto Feature, int? Position);

    private sealed record ReviewQuality(int PositiveReviewCount, int ReviewSampleSize);

    private sealed record ProductDetails(string? Description, JsonElement? Characteristics);

    private sealed record DeliveryProfileRow(
        string WbProductId,
        string SourceRegionDest,
        string? DeliveryProfileKey,
        string? DeliveryDestinationName,
        string? DeliveryDestinationCity,
        string? DeliveryDestinationAddress,
        int? TotalQuantityObserved,
        int? ProductTime1Raw,
        int? ProductTime2Raw,
        long? ProductDtypeRaw,
        string? VisibleDeliveryLabel,
        DateTime? VisibleDeliveryDate,
        DateTime ObservedAtUtc,
        long SourceLineNumber);

    private sealed record PendingAnalysis(
        Guid IdWorkspaceMarketProduct,
        decimal? CurrentPrice,
        decimal? PreviousPrice,
        decimal? PriceDelta,
        int? CurrentPosition,
        int? PreviousPosition,
        int? PositionDelta,
        int? CurrentStock,
        int? PreviousStock,
        int? StockDelta,
        int? CurrentFeedbackCount,
        int? PreviousFeedbackCount,
        int? FeedbackDelta,
        decimal? CurrentReviewRating,
        decimal? PreviousReviewRating,
        decimal? ReviewRatingDelta,
        DateTime? LatestObservedAtUtc,
        JsonDocument Signals,
        JsonDocument SimilarProducts,
        JsonDocument SimilarProductGroups,
        DateTime ComputedAtUtc);
}
