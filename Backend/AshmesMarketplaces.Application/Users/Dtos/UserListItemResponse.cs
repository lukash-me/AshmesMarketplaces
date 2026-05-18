namespace AshmesMarketplaces.Application.Users.Dtos;

public sealed record UserListItemResponse(
    Guid Id,
    Guid IdRole,
    string Login,
    string? Email,
    string Phone,
    int Status,
    DateTime DateCreate,
    DateTime DateLogin);
