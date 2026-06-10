namespace AshmesMarketplaces.Application.WorkspaceMarketProducts.Dtos;

public sealed record WorkspaceMarketProductListItemResponse(
    Guid Id,
    Guid IdWorkspace,
    Guid ParserProductRowId,
    string WbProductId,
    string? WbRootId,
    string? SourceCategory,
    string? SourceSubcategory,
    string? SourceRegionDest,
    string? SourceQuery,
    string TagKey,
    string? Note,
    string Name,
    string? BrandName,
    string? SellerName,
    string? ThumbnailUrl,
    decimal? PriceRegular,
    decimal? PriceDiscounted,
    decimal? PriceWbWallet,
    decimal? ReviewRating,
    int? FeedbackCount,
    int? PositionAbsolute,
    int? TotalQuantity,
    DateTime DateCreate,
    DateTime DateUpdate,
    DateTime? LatestObservedAtUtc);

public sealed record WorkspaceMarketProductResponse(
    Guid Id,
    Guid IdWorkspace,
    Guid IdCreatedByUser,
    Guid ParserProductRowId,
    string WbProductId,
    string? WbRootId,
    string? SourceCategory,
    string? SourceSubcategory,
    string? SourceRegionDest,
    string? SourceQuery,
    string TagKey,
    string? Note,
    string Name,
    string? BrandName,
    string? SellerName,
    string? ThumbnailUrl,
    decimal? PriceRegular,
    decimal? PriceDiscounted,
    decimal? PriceWbWallet,
    decimal? ReviewRating,
    int? FeedbackCount,
    int? PositionAbsolute,
    int? TotalQuantity,
    DateTime DateCreate,
    DateTime DateUpdate,
    DateTime? LatestObservedAtUtc);

public sealed record CreateWorkspaceMarketProductRequest(
    Guid ParserProductRowId,
    string TagKey,
    string? Note);

public sealed record UpdateWorkspaceMarketProductRequest(
    string TagKey,
    string? Note);

public sealed record WorkspaceMarketProductHistoryResponse(
    Guid Id,
    string WbProductId,
    IReadOnlyList<WorkspaceMarketProductHistoryGroupResponse> Groups);

public sealed record WorkspaceMarketProductHistoryGroupResponse(
    string Key,
    string Label,
    IReadOnlyList<WorkspaceMarketProductHistoryPointResponse> Items);

public sealed record WorkspaceMarketProductHistoryPointResponse(
    DateTime ObservedAtUtc,
    decimal? Value,
    string DisplayValue,
    string? Context);

public sealed class WorkspaceMarketProductListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public string? TagKey { get; init; }
    public Guid? ParserProductRowId { get; init; }
}
