using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public sealed class MarketHotProductsReadService : IMarketHotProductsReadService
{
    private const string StatusCompleted = "completed";
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<MarketHotProductsReadService> _logger;

    public MarketHotProductsReadService(
        ApplicationDbContext dbContext,
        ILogger<MarketHotProductsReadService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ServiceResult<HotProductsListResponse>> GetHotProductsAsync(
        HotProductsListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var nowUtc = DateTime.UtcNow;

        var runs = _dbContext.MarketRecommendationRuns
            .AsNoTracking()
            .Where(x =>
                x.Kind == MarketRecommendationRun.HotProductsKind
                && x.Status == StatusCompleted
                && x.RecommendationsCount > 0
                && (x.ValidUntilUtc == null || x.ValidUntilUtc >= nowUtc));

        if (HasItemFilters(query))
        {
            runs = runs.Where(run => ApplyItemFilters(
                    _dbContext.MarketHotProductRecommendations.AsNoTracking(),
                    query)
                .Any(item => item.IdMarketRecommendationRun == run.Id));
        }

        var run = await runs
            .OrderByDescending(x => x.CompletedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
        {
            return ServiceResult<HotProductsListResponse>.Success(
                new HotProductsListResponse(null, page, pageSize, 0, []));
        }

        var itemsQuery = ApplyItemFilters(
            _dbContext.MarketHotProductRecommendations
                .AsNoTracking()
                .Where(x => x.IdMarketRecommendationRun == run.Id),
            query);

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return ServiceResult<HotProductsListResponse>.Success(
                new HotProductsListResponse(null, page, pageSize, 0, []));
        }

        var itemRows = await itemsQuery
            .OrderBy(x => x.RankOrder)
            .ThenByDescending(x => x.Score)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var thumbnailUrls = await LoadThumbnailUrlsAsync(itemRows, cancellationToken);

        var response = new HotProductsListResponse(
            new HotProductsRunSummaryDto(
                run.Id,
                run.CompletedAtUtc ?? run.CreatedAtUtc,
                run.ValidUntilUtc,
                run.Algorithm,
                run.AlgorithmVersion,
                totalCount,
                run.WarningCount),
            page,
            pageSize,
            totalCount,
            itemRows.Select(item => MapItem(item, thumbnailUrls)).ToList());

        return ServiceResult<HotProductsListResponse>.Success(response);
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> LoadThumbnailUrlsAsync(
        IReadOnlyList<MarketHotProductRecommendation> items,
        CancellationToken cancellationToken)
    {
        var parserProductRowIds = items
            .Select(x => x.IdParserProductRow)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        if (parserProductRowIds.Count == 0)
            return new Dictionary<Guid, string?>();

        var rows = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => parserProductRowIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.ImageUrls
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.Id, x => GetFirstImageUrl(x.ImageUrls));
    }

    private static bool HasItemFilters(HotProductsListQuery query)
    {
        return !string.IsNullOrWhiteSpace(query.SourceCategory)
            || !string.IsNullOrWhiteSpace(query.SourceSubcategory)
            || !string.IsNullOrWhiteSpace(query.WbProductId)
            || !string.IsNullOrWhiteSpace(query.WbRootId);
    }

    private static IQueryable<MarketHotProductRecommendation> ApplyItemFilters(
        IQueryable<MarketHotProductRecommendation> items,
        HotProductsListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SourceCategory))
        {
            var value = query.SourceCategory.Trim();
            items = items.Where(x =>
                x.SourceCategory != null && EF.Functions.ILike(x.SourceCategory, value));
        }

        if (!string.IsNullOrWhiteSpace(query.SourceSubcategory))
        {
            var value = query.SourceSubcategory.Trim();
            items = items.Where(x =>
                x.SourceSubcategory != null && EF.Functions.ILike(x.SourceSubcategory, value));
        }

        if (!string.IsNullOrWhiteSpace(query.WbProductId))
        {
            var value = query.WbProductId.Trim();
            items = items.Where(x => x.WbProductId != null && x.WbProductId == value);
        }

        if (!string.IsNullOrWhiteSpace(query.WbRootId))
        {
            var value = query.WbRootId.Trim();
            items = items.Where(x => x.WbRootId != null && x.WbRootId == value);
        }

        return items;
    }

    private HotProductRecommendationListItemDto MapItem(
        MarketHotProductRecommendation item,
        IReadOnlyDictionary<Guid, string?> thumbnailUrls)
    {
        var thumbnailUrl = item.IdParserProductRow.HasValue
            && thumbnailUrls.TryGetValue(item.IdParserProductRow.Value, out var value)
                ? value
                : null;

        return new HotProductRecommendationListItemDto(
            item.Id,
            item.RankOrder,
            item.ProductName,
            thumbnailUrl,
            item.WbProductId,
            item.WbRootId,
            item.BrandName,
            item.SellerName,
            item.SourceCategory,
            item.SourceSubcategory,
            item.PriceSnapshot,
            item.PriceWithoutDiscountSnapshot,
            item.WalletPriceSnapshot,
            item.RatingSnapshot,
            item.FeedbackCountSnapshot,
            item.ParsedReviewCountSnapshot,
            item.ParsedReplyCountSnapshot,
            item.PositionSnapshot,
            item.PositionState,
            item.ObservedRangeLimit,
            item.TotalQuantitySnapshot,
            item.Score,
            item.Confidence,
            item.Title,
            item.Reason,
            MapFactors(item),
            item.ValidUntilUtc);
    }

    private static string? GetFirstImageUrl(JsonDocument? imageUrls)
    {
        if (imageUrls is null || imageUrls.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        return imageUrls.RootElement
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private IReadOnlyList<HotProductRecommendationFactorDto> MapFactors(MarketHotProductRecommendation item)
    {
        try
        {
            if (item.Factors.RootElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning(
                    "Hot-product recommendation factors JSON is not an array. recommendationId={RecommendationId}",
                    item.Id);
                return [];
            }

            var factors = new List<HotProductRecommendationFactorDto>();
            foreach (var element in item.Factors.RootElement.EnumerateArray())
            {
                if (TryMapFactor(element, out var factor))
                    factors.Add(factor);
            }

            return factors;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogWarning(
                ex,
                "Hot-product recommendation factors JSON could not be read. recommendationId={RecommendationId}",
                item.Id);
            return [];
        }
    }

    private static bool TryMapFactor(JsonElement element, out HotProductRecommendationFactorDto factor)
    {
        factor = null!;
        try
        {
            if (element.ValueKind != JsonValueKind.Object)
                return false;

            if (!element.TryGetProperty("code", out var codeElement)
                || codeElement.ValueKind != JsonValueKind.String
                || !element.TryGetProperty("label", out var labelElement)
                || labelElement.ValueKind != JsonValueKind.String
                || !element.TryGetProperty("weight", out var weightElement)
                || !element.TryGetProperty("direction", out var directionElement)
                || directionElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var code = codeElement.GetString();
            var label = labelElement.GetString();
            var direction = directionElement.GetString();
            if (string.IsNullOrWhiteSpace(code)
                || string.IsNullOrWhiteSpace(label)
                || string.IsNullOrWhiteSpace(direction))
            {
                return false;
            }

            decimal weight;
            if (weightElement.ValueKind == JsonValueKind.Number)
            {
                if (!weightElement.TryGetDecimal(out weight))
                    return false;
            }
            else
            {
                return false;
            }

            JsonElement? value = null;
            if (element.TryGetProperty("value", out var valueElement))
                value = valueElement.Clone();

            factor = new HotProductRecommendationFactorDto(
                code,
                label,
                value,
                weight,
                direction);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
