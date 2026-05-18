namespace AshmesMarketplaces.Application.Permissions.Dtos;

public sealed record PermissionResponse(
    Guid Id,
    Guid IdCategory,
    string Name,
    string Description,
    int Domain,
    DateTime DateCreate,
    DateTime DateUpdate);
