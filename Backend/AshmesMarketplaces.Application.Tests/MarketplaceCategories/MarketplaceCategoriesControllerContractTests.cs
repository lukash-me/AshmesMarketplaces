using Xunit;

namespace AshmesMarketplaces.Application.Tests.MarketplaceCategories;

public sealed class MarketplaceCategoriesControllerContractTests
{
    [Fact]
    public void SearchWildberriesLeaves_binds_plain_query_parameter()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "AshmesMarketplaces.API",
            "Controllers",
            "V1",
            "MarketplaceCategoriesController.cs"));

        Assert.Contains("SearchWildberriesLeaves(", source);
        Assert.Contains("[FromQuery] string? query", source);
        Assert.DoesNotContain("[FromQuery] WildberriesCategorySearchQuery query", source);
    }
}
