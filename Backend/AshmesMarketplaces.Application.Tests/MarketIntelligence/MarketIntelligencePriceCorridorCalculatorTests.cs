using AshmesMarketplaces.Application.MarketIntelligence.Services;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.MarketIntelligence;

public sealed class MarketIntelligencePriceCorridorCalculatorTests
{
    [Fact]
    public void Build_CalculatesPercentilesAndSegments()
    {
        var result = MarketIntelligencePriceCorridorCalculator.Build(
        [
            new PriceCorridorInput(100m, 1),
            new PriceCorridorInput(200m, 2),
            new PriceCorridorInput(300m, 20),
            new PriceCorridorInput(400m, 60),
            new PriceCorridorInput(500m, 120)
        ]);

        Assert.Equal(5, result.SampleSize);
        Assert.Equal(100m, result.Min);
        Assert.Equal(200m, result.P25);
        Assert.Equal(300m, result.Median);
        Assert.Equal(400m, result.P75);
        Assert.Equal(460m, result.P90);
        Assert.Equal(500m, result.Max);
        Assert.Equal(300m, result.Average);

        Assert.Equal(2, result.Segments.Single(x => x.Key == "lower").ProductsCount);
        Assert.Equal(2, result.Segments.Single(x => x.Key == "mass").ProductsCount);
        Assert.Equal(1, result.Segments.Single(x => x.Key == "premium").ProductsCount);
    }

    [Fact]
    public void Build_CalculatesTopMediansByPosition()
    {
        var result = MarketIntelligencePriceCorridorCalculator.Build(
        [
            new PriceCorridorInput(100m, 1),
            new PriceCorridorInput(500m, 8),
            new PriceCorridorInput(300m, 40),
            new PriceCorridorInput(900m, 80),
            new PriceCorridorInput(1200m, null)
        ]);

        Assert.Equal(300m, result.Top10Median);
        Assert.Equal(300m, result.Top50Median);
        Assert.Equal(400m, result.Top100Median);
    }

    [Fact]
    public void Build_CalculatesMedianRatingBySegment()
    {
        var result = MarketIntelligencePriceCorridorCalculator.Build(
        [
            new PriceCorridorInput(100m, 1, 4.2m),
            new PriceCorridorInput(200m, 2, 4.8m),
            new PriceCorridorInput(300m, 20, 5.0m),
            new PriceCorridorInput(400m, 60, 4.0m),
            new PriceCorridorInput(500m, 120, null)
        ]);

        Assert.Equal(4.5m, result.Segments.Single(x => x.Key == "lower").MedianRating);
        Assert.Equal(4.5m, result.Segments.Single(x => x.Key == "mass").MedianRating);
        Assert.Null(result.Segments.Single(x => x.Key == "premium").MedianRating);
    }

    [Fact]
    public void Build_ReturnsEmptyStateWithoutPrices()
    {
        var result = MarketIntelligencePriceCorridorCalculator.Build(
        [
            new PriceCorridorInput(null, 1),
            new PriceCorridorInput(null, 2)
        ]);

        Assert.Equal(0, result.SampleSize);
        Assert.Empty(result.Segments);
        Assert.Null(result.Median);
        Assert.NotEmpty(result.Limitations);
    }
}
