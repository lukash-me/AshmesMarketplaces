using System.Text.Json;
using System.Text.RegularExpressions;
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
    private const string DuplicateCardsKey = "duplicate_cards";
    private static readonly IReadOnlyList<HotProductsGroupDefinition> GroupDefinitions =
    [
        new("high_position_weak_reviews", "Слабые отзывы", "Карточка заметна в выдаче, но отзывы или оценка выглядят слабо."),
        new("bad_recent_reviews", "Плохие последние отзывы", "Последние отзывы требуют проверки перед выбором товара."),
        new("repeated_review_complaint", "Повторяющаяся жалоба", "В отзывах повторяется один и тот же повод для проверки."),
        new("weak_description", "Слабое описание", "Описание карточки выглядит коротким или неполным."),
        new("weak_visible_description", "Слабое описание", "У видимых карточек есть признаки слабого описания."),
        new("missing_key_specs", "Нет важных характеристик", "В карточке не хватает значимых характеристик для ниши."),
        new("expensive_without_advantage", "Высокая цена", "Цена выше выборки, а явного преимущества по отзывам или рейтингу не видно."),
        new("top_low_stock", "Низкий остаток", "Товар виден в выдаче, но наблюдаемый остаток низкий."),
        new("fast_position_growth", "Быстрый рост", "Товар заметно улучшил позицию между наблюдениями."),
        new("duplicate_cards", "Одинаковые карточки", "В нише есть несколько очень похожих карточек."),
        new("high_position_weak_card", "Слабая карточка в топе", "Карточка заметна в выдаче, но у нее есть слабые параметры."),
        new("low_review_count_top_position", "Мало отзывов в топе", "Товар высоко в выдаче, но отзывов пока мало."),
        new("good_reviews_weak_visibility", "Хорошие отзывы, слабая видимость", "У товара хорошие отзывы, но позиция в выдаче слабая.")
    ];

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
                new HotProductsListResponse(null, page, pageSize, 0, [], []));
        }

        var itemsQuery = ApplyItemFilters(
            _dbContext.MarketHotProductRecommendations
                .AsNoTracking()
                .Where(x => x.IdMarketRecommendationRun == run.Id),
            query);

        var allItemRows = await itemsQuery
            .OrderBy(x => x.RankOrder)
            .ThenByDescending(x => x.Score)
            .ToListAsync(cancellationToken);
        if (allItemRows.Count == 0)
        {
            return ServiceResult<HotProductsListResponse>.Success(
                new HotProductsListResponse(null, page, pageSize, 0, [], []));
        }

        var thumbnailUrls = await LoadThumbnailUrlsAsync(allItemRows, cancellationToken);
        var allRunItems = allItemRows.Select(item => MapItem(item, thumbnailUrls)).ToList();
        var groupedItems = ApplyGroupFilter(allRunItems, query.GroupKey);
        var totalCount = groupedItems.Count;
        var itemRows = allItemRows
            .Where(row => groupedItems.Any(item => item.Id == row.Id))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        var pageItems = itemRows.Select(item => MapItem(item, thumbnailUrls)).ToList();

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
            pageItems,
            BuildGroups(allRunItems));

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
            item.IdParserProductRow,
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

    private static IReadOnlyList<HotProductRecommendationListItemDto> ApplyGroupFilter(
        IReadOnlyList<HotProductRecommendationListItemDto> items,
        string? groupKey)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
            return items;

        var value = groupKey.Trim();
        return items
            .Where(item => item.Factors.Any(factor =>
                string.Equals(factor.Code, value, StringComparison.Ordinal)))
            .ToList();
    }

    private static IReadOnlyList<HotProductsGroupDto> BuildGroups(
        IReadOnlyList<HotProductRecommendationListItemDto> items)
    {
        var groups = new List<HotProductsGroupDto>();
        foreach (var definition in GroupDefinitions)
        {
            var groupItems = items
                .Where(item => item.Factors.Any(factor =>
                    string.Equals(factor.Code, definition.Key, StringComparison.Ordinal)))
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.RankOrder)
                .ToList();

            if (groupItems.Count == 0)
                continue;

            groups.Add(new HotProductsGroupDto(
                definition.Key,
                definition.Title,
                definition.Description,
                groupItems.Count,
                groupItems,
                definition.Key == DuplicateCardsKey ? BuildDuplicateClusters(groupItems) : []));
        }

        return groups;
    }

    private static IReadOnlyList<HotProductsDuplicateClusterDto> BuildDuplicateClusters(
        IReadOnlyList<HotProductRecommendationListItemDto> items)
    {
        var clusters = items
            .Select(item => new
            {
                Item = item,
                Key = GetDuplicateClusterKey(item)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key!, StringComparer.Ordinal)
            .Select(group =>
            {
                var clusterItems = group
                    .Select(x => x.Item)
                    .OrderBy(x => x.Position ?? int.MaxValue)
                    .ThenByDescending(x => x.FeedbackCount ?? 0)
                    .ThenBy(x => x.RankOrder)
                    .ToList();

                return new HotProductsDuplicateClusterDto(
                    group.Key,
                    GetDuplicateClusterTitle(clusterItems[0]),
                    clusterItems.Count,
                    clusterItems);
            })
            .Where(x => x.TotalCount >= 2)
            .OrderByDescending(x => x.TotalCount)
            .ThenBy(x => x.Title, StringComparer.Ordinal)
            .ToList();

        return clusters;
    }

    private static string? GetDuplicateClusterKey(HotProductRecommendationListItemDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.WbRootId))
            return $"root:{item.WbRootId.Trim()}";

        var normalized = NormalizeProductName(item.ProductName);
        return string.IsNullOrWhiteSpace(normalized) ? null : $"name:{normalized}";
    }

    private static string GetDuplicateClusterTitle(HotProductRecommendationListItemDto item)
    {
        var name = item.ProductName.Trim();
        if (name.Length <= 80)
            return name;

        return $"{name[..77]}...";
    }

    private static string NormalizeProductName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var tokens = Regex
            .Split(value.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(token => token.Length > 2)
            .Take(8);

        return string.Join(" ", tokens);
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

    private sealed record HotProductsGroupDefinition(
        string Key,
        string Title,
        string Description);
}
