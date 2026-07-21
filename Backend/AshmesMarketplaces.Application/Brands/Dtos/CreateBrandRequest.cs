namespace AshmesMarketplaces.Application.Brands.Dtos;

public sealed class CreateBrandRequest
{
    public string Name { get; init; } = string.Empty;
    public bool IsVerified { get; init; }
    public string Country { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public int SalesAmount { get; init; }
    public int RateRedemption { get; init; }
    public int Level { get; init; }
    public int Type { get; init; }
    public DateTime DateMpRegistration { get; init; }
    public DateTime DateUpdate { get; init; }
}
