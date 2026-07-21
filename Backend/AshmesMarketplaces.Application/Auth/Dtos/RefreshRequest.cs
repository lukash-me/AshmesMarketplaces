namespace AshmesMarketplaces.Application.Auth.Dtos;

public sealed class RefreshRequest
{
    public int SessionId { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
}
