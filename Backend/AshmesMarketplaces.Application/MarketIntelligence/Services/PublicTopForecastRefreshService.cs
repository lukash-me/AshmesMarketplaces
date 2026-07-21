using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;
using AshmesMarketplaces.Application.MarketRecommendations.Options;
using AshmesMarketplaces.Application.MarketRecommendations.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public sealed class PublicTopForecastRefreshService : IPublicTopForecastRefreshService
{
    private const string CompletedStatus = "completed";
    private const decimal DefaultMinProbability = 0.7m;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;
    private readonly IMarketHotProductsSnapshotBuilder _snapshotBuilder;
    private readonly IIntelligenceClient _intelligenceClient;
    private readonly IntelligenceOptions _options;

    public PublicTopForecastRefreshService(
        ApplicationDbContext dbContext,
        IMarketHotProductsSnapshotBuilder snapshotBuilder,
        IIntelligenceClient intelligenceClient,
        IOptions<IntelligenceOptions> options)
    {
        _dbContext = dbContext;
        _snapshotBuilder = snapshotBuilder;
        _intelligenceClient = intelligenceClient;
        _options = options.Value;
    }

    public async Task<ServiceResult<RecalculateTopForecastResponse>> RefreshAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return ServiceResult<RecalculateTopForecastResponse>.Unavailable("Intelligence service integration is disabled.");

        var contexts = PublicMarketIntelligenceContextCatalog.All;
        var contextSnapshots = new List<ContextSnapshot>();
        var allProducts = new List<MarketProductFeatureDto>();
        var byProductKey = new Dictionary<string, SnapshotProduct>(StringComparer.Ordinal);
        var warnings = new List<string>();

        foreach (var context in contexts)
        {
            var snapshotResult = await _snapshotBuilder.BuildAsync(
                new RecalculateHotProductsRequest(
                    context.SourceCategory,
                    context.SourceSubcategory,
                    ProductParserRunId: null,
                    RankParserRunId: null,
                    MaxProducts: 100000,
                    ForceRecalculate: true,
                    MaxRecommendations: 1000,
                    MinConfidence: null,
                    MinProductsForScoring: 5),
                _options,
                cancellationToken);

            if (!snapshotResult.IsSuccess)
            {
                warnings.Add($"{context.SourceSubcategory}: {snapshotResult.Error!.Message}");
                continue;
            }

            var snapshot = snapshotResult.Value!;
            contextSnapshots.Add(new ContextSnapshot(context, snapshot));
            foreach (var product in snapshot.Products)
            {
                allProducts.Add(product.Product);
                if (!string.IsNullOrWhiteSpace(product.Product.ProductKey))
                {
                    byProductKey[product.Product.ProductKey] = new SnapshotProduct(context, product);
                }
            }
        }

        if (allProducts.Count == 0)
            return ServiceResult<RecalculateTopForecastResponse>.NotFound("No products were found for top forecast training.");

        var requestId = $"top-forecast-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var trainRequest = new TopForecastTrainRequest(
            requestId,
            DateTime.UtcNow,
            "wildberries",
            new HotProductsIntelligenceScope(
                SourceCategory: null,
                contexts.Select(x => x.SourceSubcategory!).ToList(),
                ParserRunId: null,
                RankRunId: null,
                ReviewRunIds: []),
            allProducts,
            BuildTopForecastOptions());

        var trainResult = await _intelligenceClient.TrainTopForecastAsync(trainRequest, cancellationToken);
        if (!trainResult.IsSuccess)
            return ServiceResult<RecalculateTopForecastResponse>.Unavailable(trainResult.Error!.Message);

        var train = trainResult.Value!;
        if (!string.Equals(train.Status, CompletedStatus, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(train.ModelArtifactId))
        {
            return ServiceResult<RecalculateTopForecastResponse>.Unavailable(
                train.Warnings.Count == 0
                    ? "Top forecast model was not trained."
                    : string.Join("; ", train.Warnings));
        }

        var predictRequest = new TopForecastPredictRequest(
            $"{requestId}-predict",
            DateTime.UtcNow,
            "wildberries",
            train.ModelArtifactId!,
            allProducts,
            BuildTopForecastOptions());

        var predictResult = await _intelligenceClient.PredictTopForecastAsync(predictRequest, cancellationToken);
        if (!predictResult.IsSuccess)
            return ServiceResult<RecalculateTopForecastResponse>.Unavailable(predictResult.Error!.Message);

        var predict = predictResult.Value!;
        var createdAtUtc = DateTime.UtcNow;
        var run = new PublicTopForecastRun(
            train.ModelVersion,
            train.ModelArtifactId!,
            train.TrainedAtUtc,
            createdAtUtc);
        _dbContext.PublicTopForecastRuns.Add(run);

        var candidates = new List<(TopForecastPredictionDto Prediction, SnapshotProduct SnapshotProduct)>();
        foreach (var prediction in predict.Predictions)
        {
            if (string.IsNullOrWhiteSpace(prediction.ProductKey)
                || prediction.Top100Probability < DefaultMinProbability
                || !byProductKey.TryGetValue(prediction.ProductKey, out var snapshotProduct)
                || string.IsNullOrWhiteSpace(prediction.WbProductId))
            {
                continue;
            }

            var currentPosition = snapshotProduct.Product.Position;
            if (currentPosition.HasValue && currentPosition.Value <= 100)
                continue;

            candidates.Add((prediction, snapshotProduct));
        }

        var productRows = await LoadProductRowsAsync(
            candidates.Select(x => x.SnapshotProduct.Product.ParserProductRowId).Distinct().ToList(),
            cancellationToken);

        var savedCount = 0;
        foreach (var candidate in candidates)
        {
            var prediction = candidate.Prediction;
            var snapshotProduct = candidate.SnapshotProduct;
            var currentPosition = snapshotProduct.Product.Position;
            var context = snapshotProduct.Context;
            var entity = new PublicTopForecastPrediction(
                run.Id,
                context.SourceCategory!,
                context.SourceSubcategory!,
                context.Query!,
                context.SourceRegionDest!,
                context.Sort!,
                context.TopN,
                prediction.WbProductId!,
                createdAtUtc);

            productRows.TryGetValue(snapshotProduct.Product.ParserProductRowId, out var productRow);
            entity.SetProductSnapshot(
                snapshotProduct.Product.ParserProductRowId,
                snapshotProduct.Product.Product.WbRootId,
                snapshotProduct.Product.ProductName,
                ExtractThumbnail(productRow?.ImageUrls),
                snapshotProduct.Product.WalletPrice ?? snapshotProduct.Product.Price,
                snapshotProduct.Product.Rating,
                snapshotProduct.Product.FeedbackCount,
                snapshotProduct.Product.TotalQuantity,
                currentPosition,
                snapshotProduct.Product.PositionState,
                snapshotProduct.Product.ObservedRangeLimit,
                snapshotProduct.Product.SellerName,
                snapshotProduct.Product.BrandName);
            entity.SetPrediction(
                prediction.PredictedPosition,
                prediction.Top100Probability,
                prediction.Confidence,
                prediction.FeatureCoveragePercent,
                JsonSerializer.Serialize(prediction.Reasons, JsonOptions));

            _dbContext.PublicTopForecastPredictions.Add(entity);
            savedCount++;
        }

        var allWarnings = train.Warnings.Concat(predict.Warnings).Concat(warnings).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        run.SetSummary(
            train.SampleSize,
            train.TrainingSampleSize,
            train.ValidationSampleSize,
            train.TestSampleSize,
            train.PositiveCount,
            savedCount,
            DefaultMinProbability,
            JsonSerializer.Serialize(train.Metrics, JsonOptions),
            JsonSerializer.Serialize(new
            {
                train.FeatureNames,
                train.CategoricalFeatureNames,
                excludedFields = new[] { "position", "positionState", "observedRangeLimit" }
            }, JsonOptions),
            JsonSerializer.Serialize(allWarnings, JsonOptions));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<RecalculateTopForecastResponse>.Success(
            new RecalculateTopForecastResponse(
                run.Id,
                run.ModelVersion,
                run.ModelArtifactId,
                run.CalculatedAtUtc,
                run.SampleSize,
                run.PredictionsCount,
                allWarnings));
    }

    private static TopForecastOptions BuildTopForecastOptions() =>
        new(
            "catboost_top100_v1",
            TopThreshold: 100,
            MinProbability: DefaultMinProbability,
            RandomSeed: 42,
            TrainFraction: 0.70m,
            ValidationFraction: 0.15m,
            TestFraction: 0.15m,
            Iterations: 250);

    private async Task<IReadOnlyDictionary<Guid, ParserProductRowLite>> LoadProductRowsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new ParserProductRowLite(x.Id, x.ImageUrls))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    private static string? ExtractThumbnail(JsonDocument? imageUrls)
    {
        if (imageUrls is null)
            return null;

        var root = imageUrls.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString();
            }
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "big", "preview", "url", "image", "thumbnail" })
            {
                if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
            }
        }

        return null;
    }

    private sealed record ContextSnapshot(
        PublicMarketIntelligenceQuery Context,
        MarketHotProductsSnapshot Snapshot);

    private sealed record SnapshotProduct(
        PublicMarketIntelligenceQuery Context,
        MarketProductFeatureSnapshot Product);

    private sealed record ParserProductRowLite(
        Guid Id,
        JsonDocument? ImageUrls);
}
