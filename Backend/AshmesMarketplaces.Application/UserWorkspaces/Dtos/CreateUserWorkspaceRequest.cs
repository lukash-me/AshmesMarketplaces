namespace AshmesMarketplaces.Application.UserWorkspaces.Dtos;

public sealed class CreateUserWorkspaceRequest
{
    public Guid IdUser { get; init; }
    public Guid IdWorkspace { get; init; }
    public Guid IdRole { get; init; }
}
