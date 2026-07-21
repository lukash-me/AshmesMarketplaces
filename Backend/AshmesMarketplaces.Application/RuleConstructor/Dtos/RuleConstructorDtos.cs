namespace AshmesMarketplaces.Application.RuleConstructor.Dtos;

public static class RuleConstructorFilterStatuses
{
    public const string Active = "active";
    public const string Experimental = "experimental";
    public const string Disabled = "disabled";
}

public static class RuleConstructorFilterTones
{
    public const string Positive = "positive";
    public const string Negative = "negative";
    public const string Neutral = "neutral";
}

public static class RuleConstructorFilterVerificationStatuses
{
    public const string NotReady = "not_ready";
    public const string NeedsDataExport = "needs_data_export";
    public const string Ready = "ready";
}

public sealed record RuleConstructorFilterDto(
    string Id,
    string Group,
    string Name,
    string Description,
    IReadOnlyList<string> DataSources,
    string Status,
    string Tone,
    string VerificationStatus,
    bool RequiresReviewByUser,
    string? UnavailableReason);

public static class RuleExpressionKinds
{
    public const string Rule = "rule";
    public const string Group = "group";
}

public static class RuleGroupOperators
{
    public const string And = "and";
    public const string Or = "or";
}

public sealed record RuleExpressionDto(
    string Kind,
    string? RuleId,
    string? Operator,
    IReadOnlyList<RuleExpressionDto>? Children);

public sealed record RuleConstructorSearchRequest(
    IReadOnlyList<string> RuleIds,
    string CombineMode,
    string? SourceCategory,
    string? SourceSubcategory,
    string? Search,
    int Page,
    int PageSize,
    RuleExpressionDto? Expression = null);

public sealed record RuleConstructorCountsRequest(
    RuleExpressionDto? Expression,
    string? SourceCategory,
    string? SourceSubcategory,
    string? Search,
    IReadOnlyList<int>? ActiveGroupPath,
    IReadOnlyList<string>? RuleIds = null,
    string? CombineMode = null);

public sealed record RuleConstructorCountsResponse(
    int Total,
    IReadOnlyList<RuleConstructorRuleCountDto> RuleCounts);

public sealed record RuleConstructorRuleCountDto(
    string RuleId,
    int? Count,
    bool Available,
    bool AlreadyUsed,
    string? UnavailableReason);

public sealed record RuleConstructorSearchResponse(
    IReadOnlyList<RuleConstructorSearchItemDto> Items,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<RuleConstructorFilterDto> AppliedFilters);

public sealed record RuleConstructorSearchItemDto(
    Guid Id,
    string WbProductId,
    string? WbRootId,
    string Name,
    string? BrandName,
    string? SellerName,
    decimal? PriceDiscounted,
    int? TotalQuantity,
    decimal? ReviewRating,
    int? FeedbackCount,
    string? SourceCategory,
    string? SourceSubcategory,
    string? SourceQuery,
    string? ThumbnailUrl,
    string? PositionState,
    int? PositionAbsolute,
    int? PositionObservedRangeLimit,
    IReadOnlyList<RuleConstructorMatchedFactDto> MatchedFacts);

public sealed record RuleConstructorMatchedFactDto(
    string Id,
    string Name,
    string Description,
    string Tone,
    string? Value);
