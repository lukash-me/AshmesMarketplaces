using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AshmesMarketplaces.Application.Auth.Security;

public sealed class PasswordHashService : IPasswordHashService
{
    private static readonly object PasswordUser = new();
    private readonly PasswordHasher<object> _passwordHasher;

    public PasswordHashService()
    {
        var options = Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 210_000
        });

        _passwordHasher = new PasswordHasher<object>(options);
    }

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(PasswordUser, password);
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        try
        {
            var result = _passwordHasher.VerifyHashedPassword(PasswordUser, passwordHash, password);
            return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
