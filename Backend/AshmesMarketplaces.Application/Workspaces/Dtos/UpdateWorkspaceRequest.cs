namespace AshmesMarketplaces.Application.Workspaces.Dtos;

public sealed class UpdateWorkspaceRequest
{
    public Guid? IdBrand { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? UrlInvite { get; init; }
    public int Status { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
