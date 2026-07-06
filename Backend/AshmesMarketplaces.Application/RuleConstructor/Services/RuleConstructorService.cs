using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RuleConstructor.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.RuleConstructor.Services;

public sealed class RuleConstructorService : IRuleConstructorService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;
    private static readonly int[] WbBasketEnds =
    [
        143, 287, 431, 719, 1007, 1061, 1115, 1169, 1313, 1601, 1655, 1919,
        2045, 2189, 2405, 2621, 2837, 3053, 3269, 3485, 3701, 3917, 4133,
        4349, 4565, 4877, 5189, 5501, 5813, 6125, 6437, 6749, 7061, 7373,
        7685, 7997, 8309, 8741, 9173, 9605, 10373, 11141, 11909, 12677,
        13445, 14213
    ];

    private static readonly IReadOnlyList<RuleConstructorFilterDto> Catalog =
    [
        Active("good_recent_reviews", "Отзывы", "Хорошие последние", "Карточка подходит, если среди последних до 10 сохраненных отзывов нет оценок ниже 4. Общий рейтинг и общее количество отзывов не учитываются.", ["ParserReviewRows"]),
        Active("bad_recent_reviews", "Отзывы", "Плохие последние", "Карточка подходит, если среди последних до 10 сохраненных отзывов есть хотя бы одна оценка ниже 4. Общий рейтинг и общее количество отзывов не учитываются.", ["ParserReviewRows"]),
        Disabled("negative_reviews_without_seller_reply", "Отзывы", "Неотработанный негатив", "Показывает товары с негативными отзывами, на которые продавец не ответил.", ["ParserCurrentProductReviewEvidence"], "Нет стабильной current-проекции ответов продавца."),
        Active("low_review_count_top_position", "Отзывы", "Мало относительно топа", "Товар находится в наблюдаемом топе, но отзывов меньше обычного для выбранной ниши.", ["ParserCurrentProductRows"]),

        Active("high_price_relative_to_niche", "Цена", "Высокая относительно ниши", "Цена товара заметно выше среднего уровня в выбранной нише.", ["ParserCurrentProductRows"]),
        Disabled("price_increased", "Цена", "Выросла", "Цена товара выросла относительно предыдущего состояния.", ["ParserProductChangeEvents"], "Нужна отдельная current-проекция динамики цены."),

        Active("no_characteristics", "Оформление карточки", "Нет характеристик", "Для товара нет полученных характеристик.", ["ParserCurrentProductDetails"]),
        Active("characteristics_less_than_niche", "Оформление карточки", "Характеристик меньше относительно ниши", "У товара меньше характеристик, чем обычно у карточек выбранной ниши.", ["ParserCurrentProductDetails"]),
        Disabled("characteristics_less_than_cluster", "Оформление карточки", "Характеристик меньше относительно похожих", "У товара меньше характеристик, чем обычно у похожих товаров в кластере.", ["Cluster facts", "ParserCurrentProductDetails"], "Кластерные факты еще не заведены в current-витрину."),

        Active("in_top_700", "Позиции", "Топ 700", "Товар был найден в наблюдаемом Top 700 по запросу ниши.", ["ParserCurrentProductRows", "ParserCurrentProductRanks"]),
        Active("beyond_top_700", "Позиции", "Не топ 700", "По нише есть rank-наблюдение, но товар находится за пределами проверенного топа.", ["ParserCurrentProductRows", "ParserCurrentProductRanks"]),

        Active("low_stock", "Логистика", "Низкий остаток", "У товара низкий наблюдаемый остаток.", ["ParserCurrentProductRows", "ParserCurrentProductLogistics"]),
        Active("no_stock", "Логистика", "Нет остатка", "У товара нулевой наблюдаемый остаток.", ["ParserCurrentProductRows", "ParserCurrentProductLogistics"]),
        Disabled("slower_delivery_than_similar", "Логистика", "Медленнее похожих", "Доставка товара медленнее похожих карточек.", ["Cluster facts", "ParserCurrentProductLogistics"], "Нужен стабильный источник сравнения похожих товаров."),

        Active("short_description", "Оформление карточки", "Короткое описание", "Описание получено, но оно слишком короткое для полноценной карточки.", ["ParserCurrentProductDetails"]),
        Active("no_description", "Оформление карточки", "Нет описания", "Для товара нет полученного описания.", ["ParserCurrentProductDetails"]),
        Active("few_images", "Оформление карточки", "Мало изображений", "В карточке мало изображений по данным parser-а.", ["ParserCurrentProductRows"])
    ];

    private readonly ApplicationDbContext _dbContext;

    public RuleConstructorService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ServiceResult<IReadOnlyList<RuleConstructorFilterDto>>> GetFiltersAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ServiceResult<IReadOnlyList<RuleConstructorFilterDto>>.Success(Catalog));
    }

    public async Task<ServiceResult<RuleConstructorSearchResponse>> SearchAsync(
        RuleConstructorSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Expression is not null)
            return await SearchWithExpressionAsync(request, cancellationToken);

        var normalizedRuleIds = request.RuleIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var filtersById = Catalog.ToDictionary(x => x.Id, StringComparer.Ordinal);
        foreach (var ruleId in normalizedRuleIds)
        {
            if (!filtersById.TryGetValue(ruleId, out var filter))
                return ServiceResult<RuleConstructorSearchResponse>.BadRequest($"Фильтр '{ruleId}' не найден.");

            if (filter.Status == RuleConstructorFilterStatuses.Disabled)
                return ServiceResult<RuleConstructorSearchResponse>.BadRequest($"Фильтр '{filter.Name}' сейчас недоступен.");
        }

        var combineMode = NormalizeCombineMode(request.CombineMode);
        if (combineMode is null)
            return ServiceResult<RuleConstructorSearchResponse>.BadRequest("Режим объединения условий должен быть 'all' или 'any'.");

        var page = request.Page > 0 ? request.Page : DefaultPage;
        var pageSize = request.PageSize > 0 ? Math.Min(request.PageSize, MaxPageSize) : DefaultPageSize;

        var query = _dbContext.ParserCurrentProductRows.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.SourceCategory))
        {
            var category = request.SourceCategory.Trim();
            query = query.Where(x => x.SourceCategory == category);
        }

        if (!string.IsNullOrWhiteSpace(request.SourceSubcategory))
        {
            var subcategory = request.SourceSubcategory.Trim();
            query = query.Where(x => x.SourceSubcategory == subcategory);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x =>
                x.WbProductId.ToLower().Contains(search) ||
                x.Name.ToLower().Contains(search) ||
                (x.BrandName != null && x.BrandName.ToLower().Contains(search)) ||
                (x.SellerName != null && x.SellerName.ToLower().Contains(search)));
        }

        var products = await query
            .OrderBy(x => x.SourceSubcategory)
            .ThenBy(x => x.Name)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var ids = products.Select(x => x.WbProductId).Distinct(StringComparer.Ordinal).ToList();
        var reviewSummaries = await _dbContext.ParserCurrentProductReviewsSummaries.AsNoTracking()
            .Where(x => ids.Contains(x.WbProductId))
            .ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);
        var recentReviews = await LoadRecentReviewsAsync(ids, cancellationToken);
        var details = await _dbContext.ParserCurrentProductDetails.AsNoTracking()
            .Where(x => ids.Contains(x.WbProductId))
            .ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);

        var stats = RuleEvaluationStats.From(products, details);
        var evaluated = products
            .Select(product => Evaluate(product, reviewSummaries, recentReviews, details, stats, filtersById))
            .Where(x => Matches(x.MatchedFacts, normalizedRuleIds, combineMode))
            .ToList();

        var total = evaluated.Count;
        var items = evaluated
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => Map(x.Product, x.MatchedFacts))
            .ToList();

        var appliedFilters = normalizedRuleIds
            .Select(x => filtersById[x])
            .ToList();

        return ServiceResult<RuleConstructorSearchResponse>.Success(new RuleConstructorSearchResponse(
            items,
            total,
            page,
            pageSize,
            appliedFilters));
    }

    public async Task<ServiceResult<RuleConstructorCountsResponse>> GetCountsAsync(
        RuleConstructorCountsRequest request,
        CancellationToken cancellationToken)
    {
        var filtersById = Catalog.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var expressionResult = BuildExpression(request.Expression, request.RuleIds, request.CombineMode, filtersById);
        if (!expressionResult.IsSuccess)
            return ServiceResult<RuleConstructorCountsResponse>.BadRequest(expressionResult.Error!);

        var expression = expressionResult.Expression!;
        var evaluated = await LoadEvaluatedProductsAsync(
            request.SourceCategory,
            request.SourceSubcategory,
            request.Search,
            filtersById,
            cancellationToken);
        var total = evaluated.Count(x => ExpressionMatches(expression, x.MatchedFacts));
        var activeGroupPath = request.ActiveGroupPath ?? [];
        var ruleCounts = Catalog
            .Select(filter =>
            {
                if (filter.Status == RuleConstructorFilterStatuses.Disabled)
                {
                    return new RuleConstructorRuleCountDto(
                        filter.Id,
                        null,
                        false,
                        ExpressionContainsRule(expression, filter.Id),
                        filter.UnavailableReason);
                }

                var alreadyUsed = GroupAtPathContainsRule(expression, activeGroupPath, filter.Id);
                var candidateExpression = alreadyUsed
                    ? expression
                    : AddRuleToGroup(expression, activeGroupPath, filter.Id);
                var count = evaluated.Count(x => ExpressionMatches(candidateExpression, x.MatchedFacts));
                return new RuleConstructorRuleCountDto(filter.Id, count, true, alreadyUsed, null);
            })
            .ToList();

        return ServiceResult<RuleConstructorCountsResponse>.Success(new RuleConstructorCountsResponse(total, ruleCounts));
    }

    private async Task<ServiceResult<RuleConstructorSearchResponse>> SearchWithExpressionAsync(
        RuleConstructorSearchRequest request,
        CancellationToken cancellationToken)
    {
        var filtersById = Catalog.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var expressionResult = BuildExpression(request.Expression, request.RuleIds, request.CombineMode, filtersById);
        if (!expressionResult.IsSuccess)
            return ServiceResult<RuleConstructorSearchResponse>.BadRequest(expressionResult.Error!);

        var page = request.Page > 0 ? request.Page : DefaultPage;
        var pageSize = request.PageSize > 0 ? Math.Min(request.PageSize, MaxPageSize) : DefaultPageSize;
        var expression = expressionResult.Expression!;
        var selectedRuleIds = RuleIdsInExpression(expression).ToHashSet(StringComparer.Ordinal);
        var evaluated = await LoadEvaluatedProductsAsync(
            request.SourceCategory,
            request.SourceSubcategory,
            request.Search,
            filtersById,
            cancellationToken);
        var matched = evaluated
            .Where(x => ExpressionMatches(expression, x.MatchedFacts))
            .ToList();
        var total = matched.Count;
        var items = matched
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => Map(x.Product, x.MatchedFacts))
            .ToList();
        var appliedFilters = selectedRuleIds
            .Select(x => filtersById[x])
            .ToList();

        return ServiceResult<RuleConstructorSearchResponse>.Success(new RuleConstructorSearchResponse(
            items,
            total,
            page,
            pageSize,
            appliedFilters));
    }

    private async Task<IReadOnlyList<EvaluatedProduct>> LoadEvaluatedProductsAsync(
        string? sourceCategory,
        string? sourceSubcategory,
        string? searchText,
        IReadOnlyDictionary<string, RuleConstructorFilterDto> filtersById,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ParserCurrentProductRows.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(sourceCategory))
        {
            var category = sourceCategory.Trim();
            query = query.Where(x => x.SourceCategory == category);
        }

        if (!string.IsNullOrWhiteSpace(sourceSubcategory))
        {
            var subcategory = sourceSubcategory.Trim();
            query = query.Where(x => x.SourceSubcategory == subcategory);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim().ToLower();
            query = query.Where(x =>
                x.WbProductId.ToLower().Contains(search) ||
                x.Name.ToLower().Contains(search) ||
                (x.BrandName != null && x.BrandName.ToLower().Contains(search)) ||
                (x.SellerName != null && x.SellerName.ToLower().Contains(search)));
        }

        var products = await query
            .OrderBy(x => x.SourceSubcategory)
            .ThenBy(x => x.Name)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var ids = products.Select(x => x.WbProductId).Distinct(StringComparer.Ordinal).ToList();
        var reviewSummaries = await _dbContext.ParserCurrentProductReviewsSummaries.AsNoTracking()
            .Where(x => ids.Contains(x.WbProductId))
            .ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);
        var recentReviews = await LoadRecentReviewsAsync(ids, cancellationToken);
        var details = await _dbContext.ParserCurrentProductDetails.AsNoTracking()
            .Where(x => ids.Contains(x.WbProductId))
            .ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);

        var stats = RuleEvaluationStats.From(products, details);
        return products
            .Select(product => Evaluate(product, reviewSummaries, recentReviews, details, stats, filtersById))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<RecentReviewFact>>> LoadRecentReviewsAsync(
        IReadOnlyCollection<string> wbProductIds,
        CancellationToken cancellationToken)
    {
        if (wbProductIds.Count == 0)
            return new Dictionary<string, IReadOnlyList<RecentReviewFact>>(StringComparer.Ordinal);

        var rows = await _dbContext.ParserReviewRows.AsNoTracking()
            .Where(x => wbProductIds.Contains(x.WbProductId) && x.Rating.HasValue)
            .Select(x => new
            {
                x.WbProductId,
                x.Rating,
                x.CreatedAtOnMp,
                x.ParsedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RecentReviewFact>)group
                    .OrderByDescending(x => x.CreatedAtOnMp.HasValue)
                    .ThenByDescending(x => x.CreatedAtOnMp)
                    .ThenByDescending(x => x.ParsedAtUtc)
                    .Take(10)
                    .Select(x => new RecentReviewFact(x.Rating!.Value))
                    .ToList(),
                StringComparer.Ordinal);
    }

    private static ExpressionBuildResult BuildExpression(
        RuleExpressionDto? expression,
        IReadOnlyList<string>? legacyRuleIds,
        string? legacyCombineMode,
        IReadOnlyDictionary<string, RuleConstructorFilterDto> filtersById)
    {
        if (expression is not null)
            return NormalizeExpression(expression, filtersById);

        var combineMode = NormalizeCombineMode(legacyCombineMode);
        if (combineMode is null)
            return ExpressionBuildResult.Failure("Combine mode must be 'all' or 'any'.");

        var groupOperator = combineMode == "any" ? RuleGroupOperators.Or : RuleGroupOperators.And;
        var children = (legacyRuleIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .Select(ruleId => new RuleExpressionDto(RuleExpressionKinds.Rule, ruleId, null, null))
            .ToList();

        return NormalizeExpression(
            new RuleExpressionDto(RuleExpressionKinds.Group, null, groupOperator, children),
            filtersById);
    }

    private static ExpressionBuildResult NormalizeExpression(
        RuleExpressionDto expression,
        IReadOnlyDictionary<string, RuleConstructorFilterDto> filtersById)
    {
        var kind = expression.Kind.Trim().ToLowerInvariant();
        if (kind == RuleExpressionKinds.Rule)
        {
            var ruleId = expression.RuleId?.Trim();
            if (string.IsNullOrWhiteSpace(ruleId))
                return ExpressionBuildResult.Failure("Rule expression must include ruleId.");

            if (!filtersById.TryGetValue(ruleId, out var filter))
                return ExpressionBuildResult.Failure($"Rule '{ruleId}' was not found.");

            if (filter.Status == RuleConstructorFilterStatuses.Disabled)
                return ExpressionBuildResult.Failure($"Rule '{filter.Name}' is disabled.");

            return ExpressionBuildResult.Success(new RuleExpressionDto(RuleExpressionKinds.Rule, ruleId, null, null));
        }

        if (kind != RuleExpressionKinds.Group)
            return ExpressionBuildResult.Failure("Expression kind must be 'rule' or 'group'.");

        var groupOperator = NormalizeGroupOperator(expression.Operator);
        if (groupOperator is null)
            return ExpressionBuildResult.Failure("Group operator must be 'and' or 'or'.");

        var children = new List<RuleExpressionDto>();
        foreach (var child in expression.Children ?? [])
        {
            var childResult = NormalizeExpression(child, filtersById);
            if (!childResult.IsSuccess)
                return childResult;

            children.Add(childResult.Expression!);
        }

        return ExpressionBuildResult.Success(new RuleExpressionDto(RuleExpressionKinds.Group, null, groupOperator, children));
    }

    private static string? NormalizeGroupOperator(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return RuleGroupOperators.And;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            RuleGroupOperators.And or "all" => RuleGroupOperators.And,
            RuleGroupOperators.Or or "any" => RuleGroupOperators.Or,
            _ => null
        };
    }

    private static bool ExpressionMatches(
        RuleExpressionDto expression,
        IReadOnlyList<RuleConstructorMatchedFactDto> facts)
    {
        if (expression.Kind == RuleExpressionKinds.Rule)
        {
            return facts.Any(x => string.Equals(x.Id, expression.RuleId, StringComparison.Ordinal));
        }

        var children = expression.Children ?? [];
        if (children.Count == 0)
            return true;

        return expression.Operator == RuleGroupOperators.Or
            ? children.Any(child => ExpressionMatches(child, facts))
            : children.All(child => ExpressionMatches(child, facts));
    }

    private static IEnumerable<string> RuleIdsInExpression(RuleExpressionDto expression)
    {
        if (expression.Kind == RuleExpressionKinds.Rule && !string.IsNullOrWhiteSpace(expression.RuleId))
        {
            yield return expression.RuleId;
            yield break;
        }

        foreach (var child in expression.Children ?? [])
        {
            foreach (var ruleId in RuleIdsInExpression(child))
                yield return ruleId;
        }
    }

    private static bool ExpressionContainsRule(RuleExpressionDto expression, string ruleId)
    {
        return expression.Kind == RuleExpressionKinds.Rule
            ? string.Equals(expression.RuleId, ruleId, StringComparison.Ordinal)
            : (expression.Children ?? []).Any(child => ExpressionContainsRule(child, ruleId));
    }

    private static bool GroupAtPathContainsRule(
        RuleExpressionDto expression,
        IReadOnlyList<int> groupPath,
        string ruleId)
    {
        var group = FindGroupAtPath(EnsureGroupExpression(expression), groupPath) ?? EnsureGroupExpression(expression);
        return (group.Children ?? []).Any(child =>
            child.Kind == RuleExpressionKinds.Rule &&
            string.Equals(child.RuleId, ruleId, StringComparison.Ordinal));
    }

    private static RuleExpressionDto AddRuleToGroup(
        RuleExpressionDto expression,
        IReadOnlyList<int> groupPath,
        string ruleId)
    {
        var root = EnsureGroupExpression(expression);
        var safePath = FindGroupAtPath(root, groupPath) is null ? [] : groupPath;
        return AddRuleToGroupCore(root, safePath, 0, ruleId);
    }

    private static RuleExpressionDto AddRuleToGroupCore(
        RuleExpressionDto expression,
        IReadOnlyList<int> groupPath,
        int depth,
        string ruleId)
    {
        if (expression.Kind != RuleExpressionKinds.Group)
            return expression;

        var children = (expression.Children ?? []).ToList();
        if (depth == groupPath.Count)
        {
            if (!children.Any(child => child.Kind == RuleExpressionKinds.Rule && child.RuleId == ruleId))
                children.Add(new RuleExpressionDto(RuleExpressionKinds.Rule, ruleId, null, null));

            return new RuleExpressionDto(RuleExpressionKinds.Group, null, expression.Operator, children);
        }

        var index = groupPath[depth];
        if (index < 0 || index >= children.Count)
            return expression;

        children[index] = AddRuleToGroupCore(children[index], groupPath, depth + 1, ruleId);
        return new RuleExpressionDto(RuleExpressionKinds.Group, null, expression.Operator, children);
    }

    private static RuleExpressionDto EnsureGroupExpression(RuleExpressionDto expression)
    {
        return expression.Kind == RuleExpressionKinds.Group
            ? expression
            : new RuleExpressionDto(RuleExpressionKinds.Group, null, RuleGroupOperators.And, [expression]);
    }

    private static RuleExpressionDto? FindGroupAtPath(RuleExpressionDto expression, IReadOnlyList<int> groupPath)
    {
        var current = EnsureGroupExpression(expression);
        foreach (var index in groupPath)
        {
            var children = current.Children ?? [];
            if (index < 0 || index >= children.Count || children[index].Kind != RuleExpressionKinds.Group)
                return null;

            current = children[index];
        }

        return current;
    }

    private static string? NormalizeCombineMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "all";

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "all" or "any" ? normalized : null;
    }

    private static bool Matches(
        IReadOnlyList<RuleConstructorMatchedFactDto> facts,
        IReadOnlyList<string> ruleIds,
        string combineMode)
    {
        if (ruleIds.Count == 0)
            return true;

        var matched = facts.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        return combineMode == "all"
            ? ruleIds.All(matched.Contains)
            : ruleIds.Any(matched.Contains);
    }

    private static EvaluatedProduct Evaluate(
        ParserCurrentProductRow product,
        IReadOnlyDictionary<string, ParserCurrentProductReviewsSummary> reviewSummaries,
        IReadOnlyDictionary<string, IReadOnlyList<RecentReviewFact>> recentReviewsByProduct,
        IReadOnlyDictionary<string, ParserCurrentProductDetail> detailsByProduct,
        RuleEvaluationStats stats,
        IReadOnlyDictionary<string, RuleConstructorFilterDto> filtersById)
    {
        reviewSummaries.TryGetValue(product.WbProductId, out var reviews);
        recentReviewsByProduct.TryGetValue(product.WbProductId, out var recentReviews);
        detailsByProduct.TryGetValue(product.WbProductId, out var details);
        var detailFacts = ProductDetailFacts.From(details);
        var facts = new List<RuleConstructorMatchedFactDto>();

        var hasRecentReviewCoverage = HasCompleteReviewCoverage(reviews);
        var hasOnlyGoodRecentReviews = hasRecentReviewCoverage
            && recentReviews is { Count: > 0 }
            && recentReviews.All(x => x.Rating >= 4);
        var hasBadRecentReviews = hasRecentReviewCoverage
            && recentReviews is { Count: > 0 }
            && recentReviews.Any(x => x.Rating < 4);
        AddIf(facts, filtersById, "good_recent_reviews",
            hasOnlyGoodRecentReviews,
            $"Последние {FormatReviewCount(recentReviews?.Count ?? 0)}: все оценки 4 или 5");

        AddIf(facts, filtersById, "bad_recent_reviews",
            hasBadRecentReviews,
            $"Последние {FormatReviewCount(recentReviews?.Count ?? 0)}: есть оценка ниже 4");

        AddIf(facts, filtersById, "low_review_count_top_position",
            IsObservedTop(product) && product.FeedbackCount.HasValue && product.FeedbackCount.Value < stats.MedianFeedbackCount * 0.5m,
            $"Отзывов {product.FeedbackCount ?? 0}, медиана ниши {FormatDecimal(stats.MedianFeedbackCount)}");

        var isHighPrice = product.PriceDiscounted.HasValue
            && stats.AverageDiscountedPrice > 0
            && product.PriceDiscounted.Value >= stats.AverageDiscountedPrice * 1.25m;
        AddIf(facts, filtersById, "high_price_relative_to_niche",
            isHighPrice,
            $"Цена {FormatMoney(product.PriceDiscounted)}, средняя цена ниши {FormatMoney(stats.AverageDiscountedPrice)}");

        AddIf(facts, filtersById, "no_characteristics",
            detailFacts.CharacteristicsCount == 0,
            "Характеристики не получены или пустые");

        AddIf(facts, filtersById, "characteristics_less_than_niche",
            detailFacts.CharacteristicsCount > 0
            && stats.MedianCharacteristicsCount > 0
            && detailFacts.CharacteristicsCount < stats.MedianCharacteristicsCount * 0.6m,
            $"Характеристик {detailFacts.CharacteristicsCount}, медиана ниши {FormatDecimal(stats.MedianCharacteristicsCount)}");

        AddIf(facts, filtersById, "in_top_700",
            IsObservedTop(product),
            $"Позиция {product.PositionAbsolute}");

        AddIf(facts, filtersById, "beyond_top_700",
            string.Equals(product.PositionState, "beyondObservedRange", StringComparison.OrdinalIgnoreCase),
            $"Проверенный диапазон {product.PositionObservedRangeLimit ?? 0}");

        AddIf(facts, filtersById, "low_stock",
            product.TotalQuantity is > 0 and <= 5,
            $"Остаток {product.TotalQuantity}");

        AddIf(facts, filtersById, "no_stock",
            product.TotalQuantity == 0,
            "Остаток 0");

        AddIf(facts, filtersById, "short_description",
            detailFacts.DescriptionLength is > 0 and < 80,
            $"Длина описания {detailFacts.DescriptionLength}");

        AddIf(facts, filtersById, "no_description",
            detailFacts.DescriptionLength == 0,
            "Описание не получено или пустое");

        AddIf(facts, filtersById, "few_images",
            product.ImageCount is >= 0 and <= 1,
            $"Изображений {product.ImageCount ?? 0}");

        return new EvaluatedProduct(product, facts);
    }

    private static void AddIf(
        ICollection<RuleConstructorMatchedFactDto> facts,
        IReadOnlyDictionary<string, RuleConstructorFilterDto> filtersById,
        string filterId,
        bool condition,
        string? value)
    {
        if (!condition || !filtersById.TryGetValue(filterId, out var filter))
            return;

        facts.Add(new RuleConstructorMatchedFactDto(filter.Id, filter.Name, filter.Description, filter.Tone, value));
    }

    private static bool IsObservedTop(ParserCurrentProductRow product) =>
        string.Equals(product.PositionState, "observed", StringComparison.OrdinalIgnoreCase)
        && product.PositionAbsolute is > 0 and <= 700;

    private static bool HasCompleteReviewCoverage(ParserCurrentProductReviewsSummary? reviews) =>
        reviews is not null
        && string.Equals(reviews.CoverageStatus, "full", StringComparison.OrdinalIgnoreCase)
        && (reviews.MarketplaceFeedbackCount is null || reviews.FetchedReviewsCount >= reviews.MarketplaceFeedbackCount.Value);

    private static bool IsObservedTop100(ParserCurrentProductRow product) =>
        string.Equals(product.PositionState, "observed", StringComparison.OrdinalIgnoreCase)
        && product.PositionAbsolute is > 0 and <= 100;

    private static RuleConstructorSearchItemDto Map(
        ParserCurrentProductRow product,
        IReadOnlyList<RuleConstructorMatchedFactDto> facts)
    {
        return new RuleConstructorSearchItemDto(
            product.ProductRowId,
            product.WbProductId,
            product.WbRootId,
            product.Name,
            product.BrandName,
            product.SellerName,
            product.PriceDiscounted,
            product.TotalQuantity,
            product.ReviewRating,
            product.FeedbackCount,
            product.SourceCategory,
            product.SourceSubcategory,
            product.SourceQuery,
            FirstImageUrl(product.ImageUrlsJson) ?? BuildWbImageUrl(product.WbProductId),
            product.PositionState,
            product.PositionAbsolute,
            product.PositionObservedRangeLimit,
            facts);
    }

    private static string? FirstImageUrl(string? imageUrlsJson)
    {
        if (string.IsNullOrWhiteSpace(imageUrlsJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(imageUrlsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? BuildWbImageUrl(string wbProductId)
    {
        if (!long.TryParse(wbProductId, out var productId) || productId <= 0)
            return null;

        var volume = productId / 100000;
        var part = productId / 1000;
        var basket = CalculateWbBasket(volume);
        return $"https://basket-{basket}.wbbasket.ru/vol{volume}/part{part}/{productId}/images/big/1.webp";
    }

    private static string CalculateWbBasket(long volume)
    {
        var index = Array.BinarySearch(WbBasketEnds, (int)Math.Min(volume, int.MaxValue));
        if (index < 0)
            index = ~index;
        else
            index += 1;

        return (index + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static RuleConstructorFilterDto Active(
        string id,
        string group,
        string name,
        string description,
        IReadOnlyList<string> dataSources) =>
        new(
            id,
            group,
            name,
            description,
            dataSources,
            RuleConstructorFilterStatuses.Active,
            ToneFor(id),
            VerificationStatusFor(id),
            true,
            null);

    private static RuleConstructorFilterDto Disabled(
        string id,
        string group,
        string name,
        string description,
        IReadOnlyList<string> dataSources,
        string unavailableReason) =>
        new(
            id,
            group,
            name,
            description,
            dataSources,
            RuleConstructorFilterStatuses.Disabled,
            ToneFor(id),
            VerificationStatusFor(id),
            true,
            unavailableReason);

    private static string VerificationStatusFor(string ruleId) =>
        ruleId is "good_recent_reviews" or "bad_recent_reviews"
            ? RuleConstructorFilterVerificationStatuses.NeedsDataExport
            : RuleConstructorFilterVerificationStatuses.NotReady;

    private static string ToneFor(string ruleId) =>
        ruleId is "good_recent_reviews" or "in_top_700"
            ? RuleConstructorFilterTones.Positive
            : ruleId is "high_price_relative_to_niche" or "price_increased"
                ? RuleConstructorFilterTones.Neutral
            : RuleConstructorFilterTones.Negative;

    private static string FormatMoney(decimal? value) =>
        value.HasValue ? value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "нет данных";

    private static string FormatDecimal(decimal? value) =>
        value.HasValue ? value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "нет данных";

    private static string FormatReviewCount(int count)
    {
        var lastTwoDigits = count % 100;
        var lastDigit = count % 10;
        var word = lastTwoDigits is >= 11 and <= 14
            ? "отзывов"
            : lastDigit switch
            {
                1 => "отзыв",
                >= 2 and <= 4 => "отзыва",
                _ => "отзывов"
            };
        return $"{count} {word}";
    }

    private sealed record ExpressionBuildResult(RuleExpressionDto? Expression, string? Error)
    {
        public bool IsSuccess => Error is null;

        public static ExpressionBuildResult Success(RuleExpressionDto expression) =>
            new(expression, null);

        public static ExpressionBuildResult Failure(string error) =>
            new(null, error);
    }

    private sealed record EvaluatedProduct(
        ParserCurrentProductRow Product,
        IReadOnlyList<RuleConstructorMatchedFactDto> MatchedFacts);

    private sealed record RecentReviewFact(int Rating);

    private sealed record ProductDetailFacts(
        int DescriptionLength,
        int CharacteristicsCount)
    {
        public static ProductDetailFacts From(ParserCurrentProductDetail? details)
        {
            if (details is null || string.IsNullOrWhiteSpace(details.DetailsJson))
                return new ProductDetailFacts(0, 0);

            try
            {
                using var document = JsonDocument.Parse(details.DetailsJson);
                var root = document.RootElement;
                var description = JsonString(root, "description") ?? JsonString(root, "Description");
                var characteristicsCount = JsonArrayItemCount(root, "characteristics")
                    ?? JsonArrayItemCount(root, "Characteristics")
                    ?? JsonArrayItemCount(root, "groupedOptions")
                    ?? JsonArrayItemCount(root, "GroupedOptions")
                    ?? 0;

                return new ProductDetailFacts(description?.Trim().Length ?? 0, characteristicsCount);
            }
            catch (JsonException)
            {
                return new ProductDetailFacts(0, 0);
            }
        }

        private static string? JsonString(JsonElement root, string propertyName)
        {
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static int? JsonArrayItemCount(JsonElement root, string propertyName)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var value))
                return null;

            return CountArrayItems(value);
        }

        private static int? CountArrayItems(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                var raw = value.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    return null;

                try
                {
                    using var nested = JsonDocument.Parse(raw);
                    return CountArrayItems(nested.RootElement);
                }
                catch (JsonException)
                {
                    return null;
                }
            }

            if (value.ValueKind != JsonValueKind.Array)
                return null;

            var groupedOptionsCount = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("options", out var options)
                    && options.ValueKind == JsonValueKind.Array)
                {
                    groupedOptionsCount += options.GetArrayLength();
                }
            }

            return groupedOptionsCount > 0 ? groupedOptionsCount : value.GetArrayLength();
        }
    }

    private sealed record RuleEvaluationStats(
        decimal AverageDiscountedPrice,
        decimal MedianFeedbackCount,
        decimal MedianCharacteristicsCount)
    {
        public static RuleEvaluationStats From(
            IReadOnlyList<ParserCurrentProductRow> products,
            IReadOnlyDictionary<string, ParserCurrentProductDetail> details)
        {
            var prices = products
                .Select(x => x.PriceDiscounted)
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .ToList();
            var feedback = products
                .Select(x => x.FeedbackCount)
                .Where(x => x.HasValue)
                .Select(x => (decimal)x!.Value)
                .ToList();
            var characteristics = products
                .Select(x => details.TryGetValue(x.WbProductId, out var detail) ? ProductDetailFacts.From(detail).CharacteristicsCount : 0)
                .Where(x => x > 0)
                .Select(x => (decimal)x)
                .ToList();

            return new RuleEvaluationStats(
                prices.Count == 0 ? 0 : prices.Average(),
                Median(feedback),
                Median(characteristics));
        }

        private static decimal Median(IReadOnlyList<decimal> values)
        {
            if (values.Count == 0)
                return 0;

            var ordered = values.Order().ToList();
            var middle = ordered.Count / 2;
            return ordered.Count % 2 == 1
                ? ordered[middle]
                : (ordered[middle - 1] + ordered[middle]) / 2m;
        }
    }
}
