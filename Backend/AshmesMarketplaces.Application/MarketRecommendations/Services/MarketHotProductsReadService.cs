using System.Text.Json;
using System.Text.RegularExpressions;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Recommendations;
using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public sealed class MarketHotProductsReadService : IMarketHotProductsReadService
{
    private const string StatusCompleted = "completed";
    private const string DuplicateCardsKey = "duplicate_cards";
    private const string SlowDeliveryGroupKey = "slow_delivery";
    private const string FactorModeAll = "all";

    private static readonly HashSet<string> DeprecatedFactorCodes = new(StringComparer.Ordinal)
    {
        "high_position_weak_reviews"
    };

    private static readonly HashSet<string> ContentEvidenceFactorCodes = new(StringComparer.Ordinal)
    {
        "high_position_weak_card",
        "good_reviews_weak_card",
        "weak_description",
        "weak_visible_description",
        "missing_key_specs"
    };

    private static readonly HashSet<string> TagOnlyGroupKeys = new(StringComparer.Ordinal)
    {
        "seller_stock_slow_central_delivery",
        "top_slow_central_delivery",
        "peers_slow_region_delivery",
        "top_slow_cluster_region_delivery"
    };

    private static readonly Dictionary<string, (string Title, string Description)> GroupOverrides = new(StringComparer.Ordinal)
    {
        ["faster_than_peers_region_delivery"] = (
            "Быстрее похожих",
            "Карточка доставляется в регион быстрее медианы похожих товаров.")
    };

    private static readonly Dictionary<string, string[]> CompositeGroupFactorCodes = new(StringComparer.Ordinal)
    {
        [SlowDeliveryGroupKey] =
        [
            "seller_stock_slow_central_delivery",
            "top_slow_central_delivery",
            "top_low_stock_slow_central_delivery"
        ]
    };

    private static readonly IReadOnlyList<HotProductsGroupDefinition> GroupDefinitions =
    [
        new("bad_recent_reviews", "Плохие последние отзывы", "Оценки 3 и ниже в последних отзывах."),
        new("repeated_review_complaint", "Повторяющаяся жалоба", "В отзывах повторяется один и тот же повод для проверки."),
        new("weak_description", "Слабое описание", "Описание карточки короткое или неполное. Показывается только если описание получено."),
        new("weak_visible_description", "Слабое описание", "У видимых карточек есть проверяемые признаки слабого описания."),
        new("missing_key_specs", "Проверьте характеристики", "В полученных характеристиках не хватает значимых параметров для ниши."),
        new("expensive_without_advantage", "Высокая цена", "Цена выше похожих товаров без видимого преимущества по оценке или отзывам."),
        new("top_low_stock", "Низкий остаток", "Товар заметен в выдаче, но наблюдаемый остаток низкий."),
        new("fast_position_growth", "Быстрый рост", "Товар заметно улучшил позицию между наблюдениями."),
        new("duplicate_cards", "Одинаковые карточки", "В нише есть несколько очень похожих карточек."),
        new("high_position_weak_card", "Слабая карточка в топе", "Товар высоко в выдаче, но карточка слаба по визуалу, описанию или характеристикам."),
        new("low_review_count_top_position", "Мало отзывов в топе", "Товар высоко в выдаче, но отзывов меньше, чем у похожих товаров."),
        new("good_reviews_weak_visibility", "Хорошие отзывы, слабая видимость", "У товара хорошие отзывы, но позиция в выдаче слабая."),
        new("good_reviews_weak_card", "Хороший товар, слабая карточка", "Отзывы хорошие, но карточка выглядит неполной."),
        new("good_reviews_low_stock", "Хорошие отзывы, низкий остаток", "Покупатели оценивают товар хорошо, но наблюдаемый остаток низкий."),
        new("good_reviews_high_price", "Хорошие отзывы, высокая цена", "Отзывы хорошие, но цена выше похожих товаров."),
        new(SlowDeliveryGroupKey, "Долгая доставка", "Карточки с долгой доставкой в значимые регионы."),
        new("seller_stock_slow_central_delivery", "Долгая доставка со склада продавца", "Доставка в Центральный регион дольше послезавтра, источник - склад продавца."),
        new("top_low_stock_slow_central_delivery", "Топ, низкий остаток и долгая доставка", "Товар высоко в выдаче, остаток низкий, доставка в Центральный регион дольше послезавтра."),
        new("top_slow_cluster_region_delivery", "Топ, долгая доставка в регион кластера", "Товар высоко в выдаче, но доставка в характерный регион кластера дольше послезавтра."),
        new("top_slow_central_delivery", "Топ, долгая доставка в Центральный регион", "Товар высоко в выдаче, но доставка в московскую контрольную точку дольше послезавтра."),
        new("peers_slow_region_delivery", "Похожие доставляются с задержкой", "У похожих карточек в регионе доставка обычно дольше послезавтра."),
        new("faster_than_peers_region_delivery", "Быстрее похожих", "Карточка доставляется в регион быстрее медианы похожих товаров.")
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
        var schedule = await LoadScheduleAsync(cancellationToken);

        var runs = _dbContext.MarketRecommendationRuns
            .AsNoTracking()
            .Where(x =>
                x.Kind == MarketRecommendationRun.HotProductsKind
                && x.IdUser == null
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
                new HotProductsListResponse(null, schedule, page, pageSize, 0, [], []));
        }

        var allItemRows = await ApplyItemFilters(
                _dbContext.MarketHotProductRecommendations
                    .AsNoTracking()
                    .Where(x => x.IdMarketRecommendationRun == run.Id),
                query)
            .OrderBy(x => x.RankOrder)
            .ThenByDescending(x => x.Score)
            .ToListAsync(cancellationToken);

        if (allItemRows.Count == 0)
        {
            return ServiceResult<HotProductsListResponse>.Success(
                new HotProductsListResponse(null, schedule, page, pageSize, 0, [], []));
        }

        var thumbnailUrls = await LoadThumbnailUrlsAsync(allItemRows, cancellationToken);
        var allRunItems = allItemRows
            .Select(item => MapItem(item, thumbnailUrls))
            .Where(item => item.Factors.Count > 0)
            .ToList();
        if (allRunItems.Count == 0)
        {
            return ServiceResult<HotProductsListResponse>.Success(
                new HotProductsListResponse(null, schedule, page, pageSize, 0, [], []));
        }

        var groupedItems = ApplyGroupFilter(allRunItems, query.GroupKey);
        var filteredItems = ApplyFactorFilter(groupedItems, query);
        var totalCount = filteredItems.Count;
        var pageItemIds = filteredItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToHashSet();
        var pageItems = allItemRows
            .Where(row => pageItemIds.Contains(row.Id))
            .Select(item => MapItem(item, thumbnailUrls))
            .ToList();

        var factorCodeCount = allRunItems
            .SelectMany(x => x.Factors)
            .Select(x => x.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var response = new HotProductsListResponse(
            new HotProductsRunSummaryDto(
                run.Id,
                run.CompletedAtUtc ?? run.CreatedAtUtc,
                run.ValidUntilUtc,
                run.Algorithm,
                run.AlgorithmVersion,
                totalCount,
                run.WarningCount,
                ReadProductsSent(run.ConfigOptions) ?? run.ProductCountSent,
                factorCodeCount,
                ReadWarnings(run.RawWarnings)),
            schedule,
            page,
            pageSize,
            totalCount,
            pageItems,
            BuildGroups(allRunItems));

        return ServiceResult<HotProductsListResponse>.Success(response);
    }

    private static int? ReadProductsSent(JsonDocument? configOptions)
    {
        if (configOptions?.RootElement.ValueKind != JsonValueKind.Object)
            return null;

        if (!TryGetProperty(configOptions.RootElement, "Diagnostics", "diagnostics", out var diagnostics)
            || diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetProperty(diagnostics, "ProductsSent", "productsSent", out var productsSent)
            && productsSent.ValueKind == JsonValueKind.Number
            && productsSent.TryGetInt32(out var value))
        {
            return value;
        }

        return null;
    }

    private static IReadOnlyList<string> ReadWarnings(JsonDocument? warnings)
    {
        if (warnings is null || warnings.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return warnings.RootElement
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();
    }

    private static bool TryGetProperty(JsonElement element, string name, string camelCaseName, out JsonElement value) =>
        element.TryGetProperty(name, out value) || element.TryGetProperty(camelCaseName, out value);

    private async Task<PublicHotProductsScheduleDto?> LoadScheduleAsync(CancellationToken cancellationToken)
    {
        var schedule = await _dbContext.PublicAnalysisSchedules
            .AsNoTracking()
            .Where(x => x.ScheduleKey == PublicAnalysisSchedule.HotProductsScheduleKey)
            .FirstOrDefaultAsync(cancellationToken);
        if (schedule is null)
            return null;

        return new PublicHotProductsScheduleDto(
            schedule.LocalTime.ToString("HH:mm"),
            schedule.TimezoneId,
            schedule.NextRunAtUtc,
            schedule.LastCompletedAtUtc,
            schedule.LastStatus);
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
            .Select(x => new { x.Id, x.ImageUrls })
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
        var factorCodes = FactorCodesForGroup(value);
        return items
            .Where(item => item.Factors.Any(factor =>
                factorCodes.Contains(factor.Code)))
            .ToList();
    }

    private static IReadOnlyList<HotProductRecommendationListItemDto> ApplyFactorFilter(
        IReadOnlyList<HotProductRecommendationListItemDto> items,
        HotProductsListQuery query)
    {
        var factorKeys = query.FactorKeys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (factorKeys.Count == 0)
            return items;

        var requireAll = string.Equals(query.FactorMode?.Trim(), FactorModeAll, StringComparison.OrdinalIgnoreCase);
        return items
            .Where(item =>
            {
                var itemCodes = item.Factors
                    .Select(x => x.Code)
                    .ToHashSet(StringComparer.Ordinal);
                return requireAll
                    ? factorKeys.All(itemCodes.Contains)
                    : factorKeys.Any(itemCodes.Contains);
            })
            .ToList();
    }

    private static IReadOnlyList<HotProductsGroupDto> BuildGroups(
        IReadOnlyList<HotProductRecommendationListItemDto> items)
    {
        var groups = new List<HotProductsGroupDto>();
        foreach (var definition in GroupDefinitions)
        {
            if (TagOnlyGroupKeys.Contains(definition.Key))
                continue;

            var factorCodes = FactorCodesForGroup(definition.Key);
            var groupItems = items
                .Where(item => item.Factors.Any(factor =>
                    factorCodes.Contains(factor.Code)))
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.RankOrder)
                .ToList();

            if (groupItems.Count == 0)
                continue;

            var clusters = definition.Key == DuplicateCardsKey ? BuildDuplicateClusters(groupItems) : [];
            var title = definition.Title;
            var description = definition.Description;
            if (GroupOverrides.TryGetValue(definition.Key, out var groupOverride))
            {
                title = groupOverride.Title;
                description = groupOverride.Description;
            }

            groups.Add(new HotProductsGroupDto(
                definition.Key,
                title,
                description,
                groupItems.Count,
                definition.Key == DuplicateCardsKey && clusters.Count > 0 ? [] : groupItems,
                clusters));
        }

        return groups;
    }

    private static HashSet<string> FactorCodesForGroup(string groupKey)
    {
        if (CompositeGroupFactorCodes.TryGetValue(groupKey, out var factorCodes))
            return factorCodes.ToHashSet(StringComparer.Ordinal);

        return new HashSet<string>([groupKey], StringComparer.Ordinal);
    }

    private static IReadOnlyList<HotProductsDuplicateClusterDto> BuildDuplicateClusters(
        IReadOnlyList<HotProductRecommendationListItemDto> items)
    {
        return items
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
        return name.Length <= 80 ? name : $"{name[..77]}...";
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
                if (TryMapFactor(element, out var factor)
                    && !DeprecatedFactorCodes.Contains(factor.Code)
                    && !IsUnsupportedLegacyContentFactor(factor))
                {
                    if (IsInvalidBadRecentReviewsFactor(factor)
                        || IsInvalidLowReviewCountFactor(factor))
                    {
                        continue;
                    }

                    factors.Add(factor);
                }
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

            if (weightElement.ValueKind != JsonValueKind.Number || !weightElement.TryGetDecimal(out var weight))
                return false;

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

    private static bool IsUnsupportedLegacyContentFactor(HotProductRecommendationFactorDto factor)
    {
        return ContentEvidenceFactorCodes.Contains(factor.Code)
            && (!factor.Value.HasValue || factor.Value.Value.ValueKind != JsonValueKind.Object);
    }

    private static bool IsInvalidBadRecentReviewsFactor(HotProductRecommendationFactorDto factor)
    {
        if (!string.Equals(factor.Code, "bad_recent_reviews", StringComparison.Ordinal))
            return false;

        var serialized = $"{factor.Label} {GetFactorValueText(factor)}".ToLowerInvariant();
        if (serialized.Contains("оценка 0", StringComparison.Ordinal)
            || serialized.Contains("averagerating\":0", StringComparison.Ordinal)
            || serialized.Contains("reviewrating\":0", StringComparison.Ordinal))
        {
            return true;
        }

        if (!factor.Value.HasValue || factor.Value.Value.ValueKind != JsonValueKind.Object)
            return true;

        var value = factor.Value.Value;
        if (ReadOptionalDecimal(value, "averageRating") == 0m
            || ReadOptionalDecimal(value, "reviewRating") == 0m)
        {
            return true;
        }

        var lowRatingReviews = ReadOptionalInt(value, "lowRatingReviews") ?? 0;
        var negativeTextReviews = ReadOptionalInt(value, "negativeTextReviews") ?? 0;
        var badReviewCount = ReadOptionalInt(value, "badReviewCount") ?? 0;
        var reviewWindowSize = ReadOptionalInt(value, "reviewWindowSize") ?? 0;
        var sentimentVersion = ReadOptionalInt(value, "sentimentVersion") ?? 1;
        var reviewScope = ReadOptionalString(value, "reviewScope");
        if (reviewWindowSize <= 0)
            return true;

        if (sentimentVersion < 2)
            return true;

        if (!string.Equals(reviewScope, "product", StringComparison.Ordinal)
            && !string.Equals(reviewScope, "root", StringComparison.Ordinal))
        {
            return true;
        }

        if (negativeTextReviews > 0)
            return true;

        return badReviewCount <= 0 || lowRatingReviews <= 0;
    }

    private static bool IsInvalidLowReviewCountFactor(HotProductRecommendationFactorDto factor)
    {
        if (!string.Equals(factor.Code, "low_review_count_top_position", StringComparison.Ordinal))
            return false;

        if (!factor.Value.HasValue || factor.Value.Value.ValueKind != JsonValueKind.Object)
            return true;

        var value = factor.Value.Value;
        var reviewCount = ReadOptionalInt(value, "reviewCount");
        var peerMedianReviewCount = ReadOptionalInt(value, "peerMedianReviewCount");
        var peerSampleSize = ReadOptionalInt(value, "peerSampleSize");
        return reviewCount is null or < 0
            || peerMedianReviewCount is null or <= 0
            || peerSampleSize is null or < 5;
    }

    private static int? ReadOptionalInt(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var intValue))
            return intValue;

        return null;
    }

    private static decimal? ReadOptionalDecimal(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var decimalValue))
            return decimalValue;

        return null;
    }

    private static string? ReadOptionalString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static string GetFactorValueText(HotProductRecommendationFactorDto factor)
    {
        if (!factor.Value.HasValue)
            return string.Empty;

        try
        {
            return factor.Value.Value.GetRawText();
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private sealed record HotProductsGroupDefinition(
        string Key,
        string Title,
        string Description);
}
