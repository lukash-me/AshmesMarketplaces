namespace AshmesMarketplaces.Application.ParserBatches.Services;

public interface IParserProxySecretProtector
{
    string Protect(string value);

    string Unprotect(string protectedValue);
}
