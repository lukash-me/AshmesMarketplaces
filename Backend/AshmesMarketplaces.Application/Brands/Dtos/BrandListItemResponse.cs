namespace AshmesMarketplaces.Application.Brands.Dtos;

public sealed record BrandListItemResponse(
    Guid Id,
    string Name,
    bool IsVerified,
    string Country,
    string Manufacturer,
    int SalesAmount,
    int RateRedemption,
    int Level,
    int Type,
    DateTime DateMpRegistration,
    DateTime DateUpdate);
