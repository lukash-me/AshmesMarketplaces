namespace AshmesMarketplaces.Application.Marketplaces.Dtos;

public sealed record MarketplaceResponse(
    Guid Id,
    string Name,
    string ApiUrl,
    string ApiVersion,
    string Currency,
    string Region,
    int TypeCommission,
    string SchemeDelivery,
    DateTime DateUpdate);
