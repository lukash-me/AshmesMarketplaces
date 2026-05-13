namespace AshmesMarketplaces.Domain.Entities.Access;

public class RoleSubrole
{
    private RoleSubrole() { }

    public RoleSubrole(Guid idRole, Guid idSubrole)
    {
        if (idRole == Guid.Empty)
            throw new ArgumentException("Role id is required", nameof(idRole));

        if (idSubrole == Guid.Empty)
            throw new ArgumentException("Subrole id is required", nameof(idSubrole));

        if (idRole == idSubrole)
            throw new ArgumentException("Role and subrole must be different", nameof(idSubrole));

        IdRole = idRole;
        IdSubrole = idSubrole;
    }

    public Guid IdRole { get; private set; }
    public Guid IdSubrole { get; private set; }
}
