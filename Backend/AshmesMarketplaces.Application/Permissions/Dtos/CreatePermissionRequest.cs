namespace AshmesMarketplaces.Application.Permissions.Dtos;

public sealed class CreatePermissionRequest
{
    public Guid IdCategory { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Domain { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
