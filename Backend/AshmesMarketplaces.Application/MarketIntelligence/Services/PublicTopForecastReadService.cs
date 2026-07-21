using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public sealed class PublicTopForecastReadService : IPublicTopForecastReadService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;

    public PublicTopForecastReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PublicTopForecastResponse>> GetAsync(
        PublicTopForecastQuery query,
        CancellationToken cancellationToken)
    {
        var context = PublicMarketIntelligenceContextCatalog.Resolve(ToMarketIntelligenceQuery(query));
        if (context is null)
            return ServiceResult<PublicTopForecastResponse>.BadRequest("Ниша прогноза топа не поддерживается.");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var minProbability = Math.Clamp(query.MinProbability, 0m, 1m);

        var run = await _dbContext.PublicTopForecastRuns
            .AsNoTracking()
            .Where(x => x.Status == PublicTopForecastRun.CompletedStatus)
            .Where(x => _dbContext.PublicTopForecastPredictions.Any(p =>
                p.IdRun == x.Id
                && p.SourceCategory == context.SourceCategory
                && p.SourceSubcategory == context.SourceSubcategory
                && p.Query == context.Query
                && p.SourceRegionDest == context.SourceRegionDest
                && p.Sort == context.Sort
                && p.TopN == context.TopN))
            .OrderByDescending(x => x.CalculatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
        {
            return ServiceResult<PublicTopForecastResponse>.Success(
                new PublicTopForecastResponse(
                    MapContext(context),
                    null,
                    [],
                    page,
                    pageSize,
                    0,
                    0,
                    ["Прогноз топа еще не рассчитан."]));
        }

        var baseQuery = _dbContext.PublicTopForecastPredictions
            .AsNoTracking()
            .Where(x =>
                x.IdRun == run.Id
                && x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.Query == context.Query
                && x.SourceRegionDest == context.SourceRegionDest
                && x.Sort == context.Sort
                && x.TopN == context.TopN
                && x.Top100Probability >= minProbability);

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages > 0 && page > totalPages)
            page = totalPages;

        var predictions = await baseQuery
            .OrderByDescending(x => x.Top100Probability)
            .ThenBy(x => x.PredictedPosition ?? int.MaxValue)
            .ThenBy(x => x.CurrentPosition ?? int.MaxValue)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var response = new PublicTopForecastResponse(
            MapContext(context),
            MapRun(run),
            predictions.Select(MapItem).ToList(),
            page,
            pageSize,
            totalCount,
            totalPages,
            []);

        return ServiceResult<PublicTopForecastResponse>.Success(response);
    }

    private static PublicMarketIntelligenceQuery ToMarketIntelligenceQuery(PublicTopForecastQuery query) =>
        new()
        {
            SourceCategory = query.SourceCategory,
            SourceSubcategory = query.SourceSubcategory,
            Query = query.Query,
            SourceRegionDest = query.SourceRegionDest,
            Sort = query.Sort,
            TopN = query.TopN
        };

    private static PublicMarketContextDto MapContext(PublicMarketIntelligenceQuery context) =>
        new(
            "wildberries",
            context.SourceCategory,
            context.SourceSubcategory,
            context.Query ?? context.SourceSubcategory ?? string.Empty,
            context.SourceRegionDest,
            context.Sort,
            context.TopN);

    private static PublicTopForecastRunDto MapRun(PublicTopForecastRun run)
    {
        var warnings = DeserializeWarnings(run.WarningsJson);
        return new PublicTopForecastRunDto(
            run.Id,
            run.ModelVersion,
            run.ModelArtifactId,
            run.TrainedAtUtc,
            run.CalculatedAtUtc,
            run.SampleSize,
            run.TrainingSampleSize,
            run.ValidationSampleSize,
            run.TestSampleSize,
            run.PositiveCount,
            run.PredictionsCount,
            run.MinProbability,
            DeserializeElement(run.MetricsJson),
            DeserializeElement(run.FeatureSchemaJson),
            warnings);
    }

    private static PublicTopForecastItemDto MapItem(PublicTopForecastPrediction prediction) =>
        new(
            prediction.WbProductId,
            prediction.WbRootId,
            prediction.ProductRowId?.ToString(),
            prediction.ProductName,
            prediction.ThumbnailUrl,
            prediction.Price,
            prediction.Rating,
            prediction.FeedbackCount,
            prediction.Stock,
            prediction.CurrentPosition,
            prediction.CurrentPositionState,
            prediction.ObservedRangeLimit,
            prediction.PredictedPosition,
            prediction.Top100Probability,
            prediction.Confidence,
            prediction.SellerName,
            prediction.BrandName,
            prediction.FeatureCoveragePercent,
            DeserializeWarnings(prediction.ReasonsJson));

    private static JsonElement? DeserializeElement(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> DeserializeWarnings(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
