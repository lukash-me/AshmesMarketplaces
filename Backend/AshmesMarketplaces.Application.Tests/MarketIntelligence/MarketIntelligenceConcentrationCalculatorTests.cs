using AshmesMarketplaces.Application.MarketIntelligence.Services;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.MarketIntelligence;

public sealed class MarketIntelligenceConcentrationCalculatorTests
{
    [Fact]
    public void Build_CalculatesSellerSharesAndHhi()
    {
        var result = MarketIntelligenceConcentrationCalculator.Build(
        [
            Product("1", "r1", "Seller A", "Brand A", 1),
            Product("2", "r2", "Seller A", "Brand B", 2),
            Product("3", "r3", "Seller B", "Brand B", 3),
            Product("4", "r4", "Seller C", "Brand C", 4),
            Product("5", "r5", "Seller D", "Brand D", 5)
        ]);

        Assert.Equal(5, result.SampleSize);
        Assert.Equal(4, result.UniqueSellersCount);
        Assert.Equal(4, result.UniqueBrandsCount);
        Assert.Equal(80m, result.Top3SellersSharePercent);
        Assert.Equal(100m, result.Top5SellersSharePercent);
        Assert.Equal(0.28m, result.Hhi);
        Assert.Equal(100m, result.NormalizedConcentrationScore);
        Assert.Equal("Seller A", result.SellerLeaders[0].Name);
        Assert.Equal(2, result.SellerLeaders[0].SlotsCount);
        Assert.Equal(40m, result.SellerLeaders[0].SharePercent);
        Assert.Equal(1, result.SellerLeaders[0].BestPosition);
    }

    [Fact]
    public void Build_HandlesEmptySellerAndBrandData()
    {
        var result = MarketIntelligenceConcentrationCalculator.Build(
        [
            Product("1", "r1", null, null, 1),
            Product("2", "r2", "", " ", 2)
        ]);

        Assert.Equal(2, result.SampleSize);
        Assert.Equal(0, result.UniqueSellersCount);
        Assert.Equal(0, result.UniqueBrandsCount);
        Assert.Equal(0m, result.Top5SellersSharePercent);
        Assert.Equal(0m, result.Hhi);
        Assert.NotEmpty(result.Limitations);
    }

    [Fact]
    public void Build_CalculatesRootClustersByDistinctProducts()
    {
        var result = MarketIntelligenceConcentrationCalculator.Build(
        [
            Product("1", "root-1", "Seller A", "Brand A", 1),
            Product("1", "root-1", "Seller A", "Brand A", 2),
            Product("2", "root-1", "Seller B", "Brand A", 3),
            Product("3", "root-2", "Seller C", "Brand B", 4)
        ]);

        var cluster = Assert.Single(result.RootClusters);
        Assert.Equal("root-1", cluster.WbRootId);
        Assert.Equal(2, cluster.ProductCount);
        Assert.Equal(1, cluster.BestPosition);
        Assert.Equal(66.7m, cluster.SharePercent);
    }

    [Fact]
    public void Build_ReturnsEmptyStateWithoutProducts()
    {
        var result = MarketIntelligenceConcentrationCalculator.Build([]);

        Assert.Equal(0, result.SampleSize);
        Assert.Empty(result.SellerLeaders);
        Assert.Empty(result.BrandLeaders);
        Assert.Empty(result.RootClusters);
        Assert.NotEmpty(result.Limitations);
    }

    [Fact]
    public void Build_AddsCautiousInsightWhenSellerCoverageIsLow()
    {
        var result = MarketIntelligenceConcentrationCalculator.Build(
        [
            Product("1", "r1", "Seller A", "Brand A", 1),
            Product("2", "r2", null, "Brand B", 2),
            Product("3", "r3", null, "Brand C", 3)
        ]);

        Assert.Contains(result.Limitations, x => x.Contains("нет данных о продавцах", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("недостаточно", result.Insight, StringComparison.OrdinalIgnoreCase);
    }

    private static MarketConcentrationInput Product(
        string wbProductId,
        string? wbRootId,
        string? sellerName,
        string? brandName,
        int position)
    {
        return new MarketConcentrationInput(wbProductId, wbRootId, sellerName, brandName, position);
    }
}
