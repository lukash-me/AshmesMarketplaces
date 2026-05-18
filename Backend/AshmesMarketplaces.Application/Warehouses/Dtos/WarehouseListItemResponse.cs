namespace AshmesMarketplaces.Application.Warehouses.Dtos;

public sealed record WarehouseListItemResponse(
    Guid Id,
    Guid IdMp,
    string Name,
    string Code,
    string Region,
    string? City,
    bool IsActive,
    int Type,
    DateTime DateCreate,
    DateTime DateUpdate);
