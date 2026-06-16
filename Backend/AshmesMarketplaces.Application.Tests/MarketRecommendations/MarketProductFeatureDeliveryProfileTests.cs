using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.MarketRecommendations;

public sealed class MarketProductFeatureDeliveryProfileTests
{
    [Fact]
    public void MarketProductFeatureDto_CarriesDeliveryProfileForIntelligencePayload()
    {
        var feature = new MarketProductFeatureDto(
            ProductKey: "wildberries:100",
            WbProductId: "100",
            WbRootId: "10",
            Name: "Товар",
            BrandName: "Бренд",
            SellerName: "Продавец",
            SourceCategory: "Дом",
            SourceSubcategory: "Светильники",
            Price: 1000,
            PriceWithoutDiscount: 1200,
            WalletPrice: 950,
            Rating: 4.8m,
            FeedbackCount: 100,
            ParsedReviewCount: 10,
            ParsedReplyCount: 8,
            Position: 3,
            PositionState: "observed",
            ObservedRangeLimit: 100,
            TotalQuantity: 12,
            SnapshotAtUtc: new DateTime(2026, 06, 14, 12, 00, 00, DateTimeKind.Utc),
            Description: null,
            Characteristics: null,
            ImageCount: 5,
            ReviewSignals: null,
            DeliveryProfile: new MarketProductDeliveryProfileDto([
                new MarketProductDeliveryDestinationDto(
                    RegionKey: "central",
                    RegionName: "Центральный регион",
                    DestinationCity: "Москва",
                    DestinationAddress: "Москва, контрольный ПВЗ",
                    VisibleDeliveryLabel: "19 июня, склад продавца",
                    VisibleDeliveryDate: new DateTime(2026, 06, 19, 00, 00, 00, DateTimeKind.Utc),
                    DeliveryHours: 96,
                    DeliverySourceType: "seller_warehouse",
                    TotalQuantityObserved: 12,
                    ObservedAtUtc: new DateTime(2026, 06, 14, 12, 00, 00, DateTimeKind.Utc))
            ]));

        Assert.NotNull(feature.DeliveryProfile);
        var destination = Assert.Single(feature.DeliveryProfile.Destinations);
        Assert.Equal("central", destination.RegionKey);
        Assert.Equal("seller_warehouse", destination.DeliverySourceType);
        Assert.Equal(96, destination.DeliveryHours);
    }
}
