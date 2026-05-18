namespace AshmesMarketplaces.Application.RolePermissions.Dtos;

public sealed record RolePermissionListItemResponse(
    Guid IdRole,
    Guid IdPermission);
