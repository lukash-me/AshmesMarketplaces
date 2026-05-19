namespace AshmesMarketplaces.Application.Auth.Dtos;

public sealed record AuthPermissionResponse(
    Guid Id,
    Guid IdCategory,
    string Name,
    string Description,
    int Domain);
