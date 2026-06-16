using AshmesMarketplaces.Application.ParserIngestion.Services;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserIngestion;

public sealed class ParserMarketplaceResolverTests
{
    [Theory]
    [InlineData("wildberries")]
    [InlineData("wb")]
    [InlineData("Wildberries")]
    public void IsMatch_TreatsParserWildberriesAliasesAsWildberriesLocal(string parserMarketplace)
    {
        var marketplace = new Marketplace(
            "Wildberries Local",
            "https://www.wildberries.ru",
            "1",
            "RUB",
            "RU",
            0,
            "FBO",
            new DateTime(2026, 06, 15, 0, 0, 0, DateTimeKind.Utc));

        Assert.True(ParserMarketplaceResolver.IsMatch(marketplace, parserMarketplace));
    }
}
