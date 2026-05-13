namespace AshmesMarketplaces.Domain.Entities.Workspaces;

public class UserWorkspace
{
    private UserWorkspace() { }

    public UserWorkspace(Guid idUser, Guid idWorkspace, Guid idRole)
    {
        if (idUser == Guid.Empty)
            throw new ArgumentException("User id is required", nameof(idUser));

        if (idWorkspace == Guid.Empty)
            throw new ArgumentException("Workspace id is required", nameof(idWorkspace));

        if (idRole == Guid.Empty)
            throw new ArgumentException("Role id is required", nameof(idRole));

        IdUser = idUser;
        IdWorkspace = idWorkspace;
        IdRole = idRole;
    }

    public Guid IdUser { get; private set; }
    public Guid IdWorkspace { get; private set; }
    public Guid IdRole { get; private set; }
}
