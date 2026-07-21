namespace AshmesMarketplaces.Application.Roles.Dtos;

public sealed record RoleListItemResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime DateCreate,
    DateTime DateUpdate);
