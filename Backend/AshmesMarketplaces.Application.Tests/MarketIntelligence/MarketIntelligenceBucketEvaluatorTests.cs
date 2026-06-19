using AshmesMarketplaces.Application.MarketIntelligence.Services;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.MarketIntelligence;

public sealed class MarketIntelligenceBucketEvaluatorTests
{
    [Fact]
    public void EvaluateQuality_ReturnsStrongForRatedCompleteCard()
    {
        var result = MarketIntelligenceBucketEvaluator.EvaluateQuality(
            rating: 4.8m,
            feedbackCount: 120,
            imageCount: 5,
            description: new string('а', 120),
            characteristicsCount: 6,
            hasProduct: true,
            hasDetails: true);

        Assert.Equal("strong", result.Bucket);
        Assert.Contains(result.Reasons, x => x.Contains("сильные отзывы", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateQuality_ReturnsWeakForIncompleteCard()
    {
        var result = MarketIntelligenceBucketEvaluator.EvaluateQuality(
            rating: 4.0m,
            feedbackCount: 2,
            imageCount: 1,
            description: "коротко",
            characteristicsCount: 1,
            hasProduct: true,
            hasDetails: true);

        Assert.Equal("weak", result.Bucket);
        Assert.Contains(result.Reasons, x => x.Contains("мало отзывов", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Reasons, x => x.Contains("мало фото", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateQuality_ReturnsUnknownWithoutProductOrDetails()
    {
        var result = MarketIntelligenceBucketEvaluator.EvaluateQuality(
            rating: null,
            feedbackCount: null,
            imageCount: null,
            description: null,
            characteristicsCount: null,
            hasProduct: false,
            hasDetails: false);

        Assert.Equal("unknown", result.Bucket);
    }

    [Theory]
    [InlineData(0, "fast")]
    [InlineData(2, "fast")]
    [InlineData(3, "medium")]
    [InlineData(6, "medium")]
    [InlineData(7, "slow")]
    public void EvaluateDelivery_MapsDaysToBuckets(int deliveryDays, string expectedBucket)
    {
        var observedAt = new DateTime(2026, 06, 19, 10, 00, 00, DateTimeKind.Utc);
        var deliveryDate = observedAt.Date.AddDays(deliveryDays);

        var result = MarketIntelligenceBucketEvaluator.EvaluateDelivery(deliveryDate, observedAt);

        Assert.Equal(expectedBucket, result);
    }
}
