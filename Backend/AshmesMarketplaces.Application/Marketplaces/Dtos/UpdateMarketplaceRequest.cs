namespace AshmesMarketplaces.Application.Marketplaces.Dtos;

public sealed class UpdateMarketplaceRequest
{
    public string Name { get; init; } = string.Empty;
    public string ApiUrl { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public int TypeCommission { get; init; }
    public string SchemeDelivery { get; init; } = string.Empty;
    public DateTime DateUpdate { get; init; }
}
