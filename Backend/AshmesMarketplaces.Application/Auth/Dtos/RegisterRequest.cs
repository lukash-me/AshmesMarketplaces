namespace AshmesMarketplaces.Application.Auth.Dtos;

public sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
