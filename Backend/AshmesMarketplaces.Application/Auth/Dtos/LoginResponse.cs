namespace AshmesMarketplaces.Application.Auth.Dtos;

public sealed record LoginResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    int SessionId,
    AuthUserResponse User);
