namespace AshmesMarketplaces.Domain.Entities.Access;

public class RolePermission
{
    private RolePermission() { }

    public RolePermission(Guid idRole, Guid idPermission)
    {
        if (idRole == Guid.Empty)
            throw new ArgumentException("Role id is required", nameof(idRole));

        if (idPermission == Guid.Empty)
            throw new ArgumentException("Permission id is required", nameof(idPermission));

        IdRole = idRole;
        IdPermission = idPermission;
    }

    public Guid IdRole { get; private set; }
    public Guid IdPermission { get; private set; }
}
