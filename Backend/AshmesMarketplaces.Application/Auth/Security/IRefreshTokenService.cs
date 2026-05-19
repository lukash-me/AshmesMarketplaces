namespace AshmesMarketplaces.Application.Auth.Security;

public interface IRefreshTokenService
{
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
    bool VerifyRefreshToken(string storedHash, string refreshToken);
}
