namespace AshmesMarketplaces.Application.Products.Dtos;

public sealed record ProductImageResponse(
    Guid Id,
    string Url,
    int SortOrder,
    bool IsMain);
