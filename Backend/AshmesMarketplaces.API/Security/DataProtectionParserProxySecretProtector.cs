using AshmesMarketplaces.Application.ParserBatches.Services;
using Microsoft.AspNetCore.DataProtection;

namespace AshmesMarketplaces.API.Security;

public sealed class DataProtectionParserProxySecretProtector : IParserProxySecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionParserProxySecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("AshmesMarketplaces.ParserProxyPasswords.v1");
    }

    public string Protect(string value)
    {
        return _protector.Protect(value);
    }

    public string Unprotect(string protectedValue)
    {
        return _protector.Unprotect(protectedValue);
    }
}
