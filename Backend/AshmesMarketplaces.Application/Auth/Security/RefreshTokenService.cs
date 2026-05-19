using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AshmesMarketplaces.Application.Auth.Security;

public sealed class RefreshTokenService : IRefreshTokenService
{
    public string CreateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode(bytes.ToArray());
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(refreshToken);
        var hash = SHA256.HashData(bytes);
        return Base64UrlEncoder.Encode(hash);
    }

    public bool VerifyRefreshToken(string storedHash, string refreshToken)
    {
        var refreshTokenHash = HashRefreshToken(refreshToken);
        var storedHashBytes = System.Text.Encoding.UTF8.GetBytes(storedHash);
        var refreshTokenHashBytes = System.Text.Encoding.UTF8.GetBytes(refreshTokenHash);

        return storedHashBytes.Length == refreshTokenHashBytes.Length
            && CryptographicOperations.FixedTimeEquals(storedHashBytes, refreshTokenHashBytes);
    }
}
