namespace AshmesMarketplaces.Application.RoleSubroles.Dtos;

public sealed class CreateRoleSubroleRequest
{
    public Guid IdRole { get; init; }
    public Guid IdSubrole { get; init; }
}
