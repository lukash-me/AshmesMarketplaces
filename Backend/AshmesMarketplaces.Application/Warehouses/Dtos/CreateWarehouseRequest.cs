namespace AshmesMarketplaces.Application.Warehouses.Dtos;

public sealed class CreateWarehouseRequest
{
    public Guid IdMp { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? Address { get; init; }
    public string? Latitude { get; init; }
    public string? Longitude { get; init; }
    public bool IsActive { get; init; }
    public int Type { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
