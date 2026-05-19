namespace AshmesMarketplaces.Application.Auth.Security;

public interface IPasswordHashService
{
    string HashPassword(string password);
    bool VerifyPassword(string passwordHash, string password);
}
