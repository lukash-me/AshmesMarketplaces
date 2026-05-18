namespace AshmesMarketplaces.Application.Products.Dtos;

public sealed record ProductVideoResponse(
    Guid Id,
    string Url,
    int SortOrder);
