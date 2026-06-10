using System.Text.Json;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;
using AshmesMarketplaces.Application.MarketRecommendations.Services;
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
    private const int MaxSimilarProducts = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

        return ServiceResult<WorkspaceOverviewResponse>.Success(MapOverview(products, run, analyses));
    }

    public async Task<ServiceResult<WorkspaceOverviewRecalculateResponse>> RecalculateAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var access = await EnsureWorkspaceAccessAsync(workspaceId, cancellationToken);
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

        foreach (var product in products)
        {
            var history = await LoadHistoryAsync(product, cancellationToken);
            var candidates = await LoadSimilarCandidatesAsync(product, products, cancellationToken);
            var requestId = $"workspace-overview-{workspaceId:N}-{product.Id:N}-{startedAt:yyyyMMddHHmmss}";
            var intelligence = await _intelligenceClient.AnalyzeWorkspaceProductAsync(
                new WorkspaceProductAnalysisIntelligenceRequest(
                    requestId,
                    startedAt,
                    MarketplaceWildberries,
                    BuildFeature(product, history),
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
                        x.Confidence))
                    .ToArray();
                similarProducts = MapSimilarProducts(intelligence.Value.SimilarProducts ?? [], candidates);
                similarProductGroups = MapSimilarProductGroups(intelligence.Value.SimilarProductGroups ?? [], candidates);
                if (similarProductGroups.Length == 0 && similarProducts.Length > 0)
                    similarProductGroups = BuildFallbackSimilarProductGroups(product, history, similarProducts);
            }
            else if (intelligence.Error is not null)
            {
                warnings.Add($"Товар {product.WbProductId}: {intelligence.Error.Message}");
            }

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

    private async Task<WorkspaceMarketProductAnalysisRun?> LoadLatestRunAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkspaceMarketProductAnalysisRuns
            .AsNoTracking()
            .Where(x => x.IdWorkspace == workspaceId)
            .OrderByDescending(x => x.CompletedAtUtc ?? x.StartedAtUtc)
            .ThenByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private WorkspaceOverviewResponse MapOverview(
        IReadOnlyList<WorkspaceMarketProduct> products,
        WorkspaceMarketProductAnalysisRun? run,
        IReadOnlyDictionary<Guid, WorkspaceMarketProductAnalysis> analyses)
    {
        var items = products.Select(product =>
        {
            analyses.TryGetValue(product.Id, out var analysis);
            return MapProduct(product, analysis);
        }).ToList();
        var signalCount = items.Sum(x => x.Signals.Count);
        var similarCount = items.Sum(x => x.SimilarProducts.Count);

        return new WorkspaceOverviewResponse(
            run is null ? null : MapRun(run),
            products.Count,
            signalCount,
            similarCount,
            new WorkspaceOverviewGroupDto(
                WorkspaceMarketProduct.CompetitorTag,
                "Конкуренты",
                items.Count(x => x.TagKey == WorkspaceMarketProduct.CompetitorTag),
                items.Where(x => x.TagKey == WorkspaceMarketProduct.CompetitorTag).ToList()),
            new WorkspaceOverviewGroupDto(
                WorkspaceMarketProduct.IdeaTag,
                "Идеи",
                items.Count(x => x.TagKey == WorkspaceMarketProduct.IdeaTag),
                items.Where(x => x.TagKey == WorkspaceMarketProduct.IdeaTag).ToList()));
    }

    private WorkspaceOverviewProductDto MapProduct(WorkspaceMarketProduct product, WorkspaceMarketProductAnalysis? analysis)
    {
        var signals = analysis is null ? [] : DeserializeSignals(analysis.Signals);
        var similar = analysis is null ? [] : DeserializeSimilarProducts(analysis.SimilarProducts);
        var similarGroups = analysis is null ? [] : DeserializeSimilarProductGroups(analysis.SimilarProductGroups);

        return new WorkspaceOverviewProductDto(
            product.Id,
            product.ParserProductRowId,
            product.WbProductId,
            product.WbRootId,
            product.TagKey,
            product.Name,
            product.BrandName,
            product.SellerName,
            product.ThumbnailUrl,
            product.SourceCategory,
            product.SourceSubcategory,
            analysis?.CurrentPrice ?? CurrentPrice(product),
            analysis?.CurrentPosition ?? product.PositionAbsolute,
            analysis?.CurrentStock ?? product.TotalQuantity,
            analysis?.CurrentFeedbackCount ?? product.FeedbackCount,
            analysis?.CurrentReviewRating ?? product.ReviewRating,
            analysis?.LatestObservedAtUtc,
            Change("price", "Цена", analysis?.CurrentPrice, analysis?.PreviousPrice, analysis?.PriceDelta, "₽"),
            Change("position", "Позиция", analysis?.CurrentPosition, analysis?.PreviousPosition, analysis?.PositionDelta, null, lowerIsBetter: true),
            Change("stock", "Остатки", analysis?.CurrentStock, analysis?.PreviousStock, analysis?.StockDelta, null),
            Change("feedbacks", "Отзывы", analysis?.CurrentFeedbackCount, analysis?.PreviousFeedbackCount, analysis?.FeedbackDelta, null),
            Change("reviewRating", "Оценка", analysis?.CurrentReviewRating, analysis?.PreviousReviewRating, analysis?.ReviewRatingDelta, null),
            signals,
            similar,
            similarGroups);
    }

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

    private async Task<ProductHistory> LoadHistoryAsync(WorkspaceMarketProduct product, CancellationToken cancellationToken)
    {
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
        var excluded = workspaceProducts.Select(x => x.WbProductId).Append(product.WbProductId).ToHashSet(StringComparer.Ordinal);
        var rows = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => x.SourceSubcategory == product.SourceSubcategory
                && x.SourceRegionDest == product.SourceRegionDest
                && !excluded.Contains(x.WbProductId))
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
                && x.SourceRegionDest == product.SourceRegionDest)
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

        return latestRows
            .Select(row => new CandidateFeature(row, BuildFeature(row), positions.GetValueOrDefault(row.WbProductId)))
            .ToList();
    }

    private static MarketProductFeatureDto BuildFeature(WorkspaceMarketProduct product, ProductHistory history) =>
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
            history.LatestObservedAtUtc);

    private static MarketProductFeatureDto BuildFeature(ParserProductRow row) =>
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
            row.ParsedAtUtc);

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
        if (workspaceId == Guid.Empty)
            return ServiceResult.BadRequest("Workspace id is required.");

        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            return ServiceResult.Unauthorized("Authentication is required.");

        var exists = await _dbContext.Workspaces.AsNoTracking().AnyAsync(x => x.Id == workspaceId, cancellationToken);
        if (!exists)
            return ServiceResult.NotFound("Workspace was not found.");

        var hasAccess = await _dbContext.UserWorkspaces
            .AsNoTracking()
            .AnyAsync(x => x.IdWorkspace == workspaceId && x.IdUser == _currentUser.UserId.Value, cancellationToken);

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

    private static JsonDocument ToJsonDocument<T>(T value) =>
        JsonSerializer.SerializeToDocument(value, JsonOptions);

    private static JsonDocument CloneJson(JsonDocument document) =>
        JsonDocument.Parse(document.RootElement.GetRawText());

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

    private sealed record CandidateFeature(ParserProductRow Row, MarketProductFeatureDto Feature, int? Position);

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
