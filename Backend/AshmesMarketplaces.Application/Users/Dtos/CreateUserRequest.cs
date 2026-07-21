namespace AshmesMarketplaces.Application.Users.Dtos;

public sealed class CreateUserRequest
{
    public Guid IdRole { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string Phone { get; init; } = string.Empty;
    public int Status { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateLogin { get; init; }
}
