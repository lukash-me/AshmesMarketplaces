namespace AshmesMarketplaces.Application.Warehouses.Dtos;

public sealed record WarehouseResponse(
    Guid Id,
    Guid IdMp,
    string Name,
    string Code,
    string Region,
    string? City,
    string? Address,
    string? Latitude,
    string? Longitude,
    bool IsActive,
    int Type,
    DateTime DateCreate,
    DateTime DateUpdate);
