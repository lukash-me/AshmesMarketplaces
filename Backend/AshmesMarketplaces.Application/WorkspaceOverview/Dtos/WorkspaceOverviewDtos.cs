namespace AshmesMarketplaces.Application.WorkspaceOverview.Dtos;

public sealed record WorkspaceOverviewResponse(
    WorkspaceOverviewRunDto? LastAnalysis,
    int WorkspaceProductCount,
    int SignalCount,
    int SimilarProductCount,
    WorkspaceOverviewGroupDto NewItems,
    WorkspaceOverviewGroupDto Competitors,
    WorkspaceOverviewGroupDto Ideas);

public sealed record WorkspaceOverviewRunDto(
    Guid Id,
    string Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string Algorithm,
    string AlgorithmVersion,
    string ModelVersion,
    IReadOnlyList<string> Warnings,
    string? ErrorMessage);

public sealed record WorkspaceOverviewGroupDto(
    string Key,
    string Label,
    int Count,
    IReadOnlyList<WorkspaceOverviewProductDto> Products);

public sealed record WorkspaceOverviewProductDto(
    Guid Id,
    Guid ParserProductRowId,
    string WbProductId,
    string? WbRootId,
    string TagKey,
    string? Note,
    string Name,
    string? BrandName,
    string? SellerName,
    string? ThumbnailUrl,
    string? SourceCategory,
    string? SourceSubcategory,
    decimal? CurrentPrice,
    int? CurrentPosition,
    int? CurrentStock,
    int? CurrentFeedbackCount,
    decimal? CurrentReviewRating,
    DateTime? LatestObservedAtUtc,
    WorkspaceOverviewChangeDto PriceChange,
    WorkspaceOverviewChangeDto PositionChange,
    WorkspaceOverviewChangeDto StockChange,
    WorkspaceOverviewChangeDto FeedbackChange,
    WorkspaceOverviewChangeDto ReviewRatingChange,
    IReadOnlyList<WorkspaceOverviewSignalDto> Signals,
    IReadOnlyList<WorkspaceOverviewSimilarProductDto> SimilarProducts,
    IReadOnlyList<WorkspaceOverviewSimilarProductGroupDto> SimilarProductGroups);

public sealed record WorkspaceOverviewChangeDto(
    string Key,
    string Label,
    decimal? CurrentValue,
    decimal? PreviousValue,
    decimal? Delta,
    string DisplayValue,
    string State);

public sealed record WorkspaceOverviewSignalDto(
    string Code,
    string Severity,
    string Title,
    string Description,
    IReadOnlyList<string> MetricFacts,
    decimal Confidence);

public sealed record WorkspaceOverviewSimilarProductDto(
    string ProductKey,
    Guid? ParserProductRowId,
    string? WbProductId,
    string? WbRootId,
    string Name,
    string? BrandName,
    string? SellerName,
    string? ThumbnailUrl,
    string? SourceSubcategory,
    decimal? Price,
    decimal? Rating,
    int? FeedbackCount,
    int? Position,
    int? TotalQuantity,
    decimal SimilarityScore,
    string Reason);

public sealed record WorkspaceOverviewSimilarProductGroupDto(
    string Key,
    string Title,
    string Description,
    IReadOnlyList<WorkspaceOverviewSimilarProductGroupItemDto> Items);

public sealed record WorkspaceOverviewSimilarProductGroupItemDto(
    WorkspaceOverviewSimilarProductDto Product,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> Tags);

public sealed record WorkspaceOverviewRecalculateResponse(
    WorkspaceOverviewRunDto Run,
    int ProductCount,
    int SignalCount,
    int SimilarProductCount,
    IReadOnlyList<string> Warnings);

public sealed record WorkspaceOverviewMarkViewedRequest(
    IReadOnlyList<Guid> ProductIds);

public sealed record WorkspaceOverviewMarkViewedResponse(
    int UpdatedCount,
    DateTime ViewedAtUtc);
