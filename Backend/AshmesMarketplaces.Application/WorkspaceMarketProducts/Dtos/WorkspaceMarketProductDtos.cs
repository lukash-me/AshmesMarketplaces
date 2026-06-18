namespace AshmesMarketplaces.Application.WorkspaceMarketProducts.Dtos;

public sealed record WorkspaceMarketProductListItemResponse(
    Guid Id,
    Guid IdWorkspace,
    Guid? ParserProductRowId,
    string? WbProductId,
    string? WbRootId,
    string SourceType,
    bool IsDemo,
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
    decimal? CostPrice,
    string? Description,
    string? CharacteristicsJson,
    string? SupplierName,
    string? SupplierUrl,
    IReadOnlyList<WorkspaceMarketProductMediaResponse> Media,
    DateTime DateCreate,
    DateTime DateUpdate,
    DateTime? LatestObservedAtUtc);

public sealed record WorkspaceMarketProductResponse(
    Guid Id,
    Guid IdWorkspace,
    Guid IdCreatedByUser,
    Guid? ParserProductRowId,
    string? WbProductId,
    string? WbRootId,
    string SourceType,
    bool IsDemo,
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
    decimal? CostPrice,
    string? Description,
    string? CharacteristicsJson,
    string? SupplierName,
    string? SupplierUrl,
    IReadOnlyList<WorkspaceMarketProductMediaResponse> Media,
    DateTime DateCreate,
    DateTime DateUpdate,
    DateTime? LatestObservedAtUtc);

public sealed record WorkspaceMarketProductMediaResponse(
    Guid Id,
    string Url,
    string FileName,
    string ContentType,
    int SortOrder,
    string Kind,
    DateTime UploadedAtUtc);

public sealed record CreateWorkspaceMarketProductRequest(
    Guid ParserProductRowId,
    string TagKey,
    string? Note);

public sealed class CreateDemoWorkspaceMarketProductRequest
{
    public string Name { get; init; } = string.Empty;
    public string SourceCategory { get; init; } = string.Empty;
    public string SourceSubcategory { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? CostPrice { get; init; }
    public string? Description { get; init; }
    public string? CharacteristicsJson { get; init; }
    public string? SupplierName { get; init; }
    public string? SupplierUrl { get; init; }
    public string? TagKey { get; init; }
    public string? Note { get; init; }
}

public sealed record UpdateWorkspaceMarketProductRequest(
    string TagKey,
    string? Note);

public sealed record WorkspaceMarketProductHistoryResponse(
    Guid Id,
    string? WbProductId,
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
