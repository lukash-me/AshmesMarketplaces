namespace AshmesMarketplaces.Application.Roles.Dtos;

public sealed record RoleResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime DateCreate,
    DateTime DateUpdate);
