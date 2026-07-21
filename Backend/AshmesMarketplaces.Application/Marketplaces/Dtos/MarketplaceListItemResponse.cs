namespace AshmesMarketplaces.Application.Marketplaces.Dtos;

public sealed record MarketplaceListItemResponse(
    Guid Id,
    string Name,
    string Currency,
    string Region,
    int TypeCommission,
    string SchemeDelivery,
    DateTime DateUpdate);
