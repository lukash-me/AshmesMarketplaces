namespace AshmesMarketplaces.Application.Auth.Security;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);
