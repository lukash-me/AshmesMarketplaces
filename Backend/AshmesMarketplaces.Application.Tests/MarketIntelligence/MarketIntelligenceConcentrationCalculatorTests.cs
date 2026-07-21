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
    public void Build_ReturnsCountPositionAndReviewsRankings()
    {
        var result = MarketIntelligenceConcentrationCalculator.Build(
        [
            Product("1", "root-count", "Seller Count", "Brand Count", null, 5),
            Product("2", "root-count", "Seller Count", "Brand Count", null, 10),
            Product("3", "root-count", "Seller Count", "Brand Count", null, 15),
            Product("4", "root-top", "Seller Top", "Brand Top", 2, 20),
            Product("5", "root-reviews", "Seller Reviews", "Brand Reviews", 30, 1_000),
            Product("6", "root-reviews", "Seller Reviews", "Brand Reviews", null, 800)
        ]);

        var count = Assert.Single(result.Rankings, x => x.Key == "count");
        Assert.Equal("Seller Count", count.SellerLeaders[0].Name);
        Assert.Equal(30, count.SellerLeaders[0].FeedbackCount);
        Assert.Equal(0, count.SellerLeaders[0].RankedSlotsCount);

        var position = Assert.Single(result.Rankings, x => x.Key == "position");
        Assert.Equal("Seller Top", position.SellerLeaders[0].Name);
        Assert.Equal(2, position.SellerLeaders[0].BestPosition);
        Assert.All(position.SellerLeaders, x => Assert.NotNull(x.BestPosition));

        var reviews = Assert.Single(result.Rankings, x => x.Key == "reviews");
        Assert.Equal("Seller Reviews", reviews.SellerLeaders[0].Name);
        Assert.Equal(1_800, reviews.SellerLeaders[0].FeedbackCount);
    }

    [Fact]
    public void Build_CalculatesTop100Share()
    {
        var result = MarketIntelligenceConcentrationCalculator.Build(
        [
            Product("1", "root-top", "Seller A", "Brand A", 1),
            Product("2", "root-top", "Seller A", "Brand B", 100),
            Product("3", "root-outside", "Seller A", "Brand A", 101),
            Product("4", "root-other", "Seller B", "Brand A", null)
        ]);

        var seller = Assert.Single(result.SellerLeaders, x => x.Name == "Seller A");
        Assert.Equal(2m, seller.Top100SharePercent);

        var brand = Assert.Single(result.BrandLeaders, x => x.Name == "Brand B");
        Assert.Equal(1m, brand.Top100SharePercent);

        var root = Assert.Single(result.RootClusters, x => x.WbRootId == "root-top");
        Assert.Equal(2m, root.Top100SharePercent);
    }

    [Fact]
    public void Build_SortsPositionRankingByTop100ShareThenBestPosition()
    {
        var result = MarketIntelligenceConcentrationCalculator.Build(
        [
            Product("1", "root-small", "Seller Small", "Brand Small", 1),
            Product("2", "root-wide", "Seller Wide", "Brand Wide", 5),
            Product("3", "root-wide", "Seller Wide", "Brand Wide", 6),
            Product("4", "root-wide", "Seller Wide", "Brand Wide", 7),
            Product("5", "root-tie-a", "Seller Tie A", "Brand Tie A", 20),
            Product("6", "root-tie-b", "Seller Tie B", "Brand Tie B", 10)
        ]);

        var position = Assert.Single(result.Rankings, x => x.Key == "position");

        Assert.Equal("Seller Wide", position.SellerLeaders[0].Name);
        Assert.Equal(3m, position.SellerLeaders[0].Top100SharePercent);
        Assert.Equal("Seller Small", position.SellerLeaders[1].Name);
        Assert.Equal("Seller Tie B", position.SellerLeaders[2].Name);
        Assert.Equal("Seller Tie A", position.SellerLeaders[3].Name);
    }

    [Fact]
    public void Build_CalculatesFeedbackShare()
    {
        var result = MarketIntelligenceConcentrationCalculator.Build(
        [
            Product("1", "root-a", "Seller A", "Brand A", 1, 100),
            Product("2", "root-a", "Seller A", "Brand B", 2, 200),
            Product("3", "root-b", "Seller B", "Brand B", 3, 700)
        ]);

        var reviews = Assert.Single(result.Rankings, x => x.Key == "reviews");
        var seller = Assert.Single(reviews.SellerLeaders, x => x.Name == "Seller A");
        var brand = Assert.Single(reviews.BrandLeaders, x => x.Name == "Brand A");
        var root = Assert.Single(reviews.RootClusters, x => x.WbRootId == "root-a");

        Assert.Equal(30m, seller.FeedbackSharePercent);
        Assert.Equal(10m, brand.FeedbackSharePercent);
        Assert.Equal(30m, root.FeedbackSharePercent);
    }

    [Fact]
    public void Build_TreatsZeroPositionAsOutsideObservedRange()
    {
        var result = MarketIntelligenceConcentrationCalculator.Build(
        [
            Product("1", "root-1", "Seller Outside", "Brand A", 0, 50),
            Product("2", "root-2", "Seller Inside", "Brand B", 7, 10)
        ]);

        var count = Assert.Single(result.Rankings, x => x.Key == "count");
        var outsideSeller = Assert.Single(count.SellerLeaders, x => x.Name == "Seller Outside");
        Assert.Null(outsideSeller.BestPosition);
        Assert.Equal(0, outsideSeller.RankedSlotsCount);

        var position = Assert.Single(result.Rankings, x => x.Key == "position");
        Assert.DoesNotContain(position.SellerLeaders, x => x.Name == "Seller Outside");
        Assert.Equal("Seller Inside", position.SellerLeaders[0].Name);
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
        int? position,
        int? feedbackCount = null)
    {
        return new MarketConcentrationInput(wbProductId, wbRootId, sellerName, brandName, position, feedbackCount);
    }
}
