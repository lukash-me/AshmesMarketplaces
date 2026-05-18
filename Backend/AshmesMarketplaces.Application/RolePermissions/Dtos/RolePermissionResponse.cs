namespace AshmesMarketplaces.Application.RolePermissions.Dtos;

public sealed record RolePermissionResponse(
    Guid IdRole,
    Guid IdPermission);
