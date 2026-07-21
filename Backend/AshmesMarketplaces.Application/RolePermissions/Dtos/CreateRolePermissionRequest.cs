namespace AshmesMarketplaces.Application.RolePermissions.Dtos;

public sealed class CreateRolePermissionRequest
{
    public Guid IdRole { get; init; }
    public Guid IdPermission { get; init; }
}
