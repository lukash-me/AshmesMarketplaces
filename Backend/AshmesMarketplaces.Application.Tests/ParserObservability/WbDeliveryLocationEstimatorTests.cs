using AshmesMarketplaces.Application.ParserObservability.Services;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserObservability;

public sealed class WbDeliveryLocationEstimatorTests
{
    [Fact]
    public void Estimate_ReturnsMoscowHighConfidence_WhenMoscowIsFastAndSpreadIsLarge()
    {
        var estimate = WbDeliveryLocationEstimator.Estimate(
        [
            Signal("Москва", "Завтра, склад WB", 26, quantity: 48),
            Signal("Санкт-Петербург", "Послезавтра, склад WB", 39, quantity: 48),
            Signal("Казань", "Послезавтра, склад WB", 44, quantity: 48),
            Signal("Екатеринбург", "17 июня, склад WB", 52, quantity: 48),
            Signal("Новосибирск", "18 июня, склад WB", 87, quantity: 48),
            Signal("Хабаровск", "21 июня, склад WB", 168, quantity: 48)
        ]);

        Assert.Equal("estimated", estimate.Status);
        Assert.Equal("moscow_central", estimate.ZoneKey);
        Assert.Equal("Москва / Центральный регион", estimate.ZoneTitle);
        Assert.Equal("high", estimate.Confidence);
        Assert.Equal("Москва", estimate.NearestDestinationName);
        Assert.Equal(26, estimate.NearestDeliveryHours);
        Assert.Equal("Санкт-Петербург", estimate.SecondDestinationName);
        Assert.Equal(39, estimate.SecondDeliveryHours);
        Assert.Equal("Хабаровск", estimate.FarthestDestinationName);
        Assert.Equal(142, estimate.DeliverySpreadHours);
        Assert.Contains("До Москвы: Завтра, склад WB", estimate.Evidence);
        Assert.Contains("Разброс по точкам: 142 ч", estimate.Evidence);
    }

    [Fact]
    public void Estimate_ReturnsLowConfidence_WhenNearestPointsAreClose()
    {
        var estimate = WbDeliveryLocationEstimator.Estimate(
        [
            Signal("Москва", "Завтра, склад WB", 26, quantity: 48),
            Signal("Санкт-Петербург", "Завтра, склад WB", 29, quantity: 48),
            Signal("Казань", "Послезавтра, склад WB", 43, quantity: 48)
        ]);

        Assert.Equal("estimated", estimate.Status);
        Assert.Equal("low", estimate.Confidence);
        Assert.Contains("Ближайшие точки отличаются меньше чем на 6 ч", estimate.Evidence);
    }

    [Fact]
    public void Estimate_ReturnsInsufficientData_WhenLessThanThreeValidSignals()
    {
        var estimate = WbDeliveryLocationEstimator.Estimate(
        [
            Signal("Москва", "Завтра, склад WB", 26, quantity: 48),
            Signal("Хабаровск", "21 июня, склад WB", 168, quantity: 0),
            Signal("Казань", "Дата не рассчитана", null, quantity: 48)
        ]);

        Assert.Equal("insufficient_data", estimate.Status);
        Assert.Null(estimate.ZoneKey);
        Assert.Null(estimate.Confidence);
        Assert.Contains("Недостаточно контрольных точек с рассчитанной доставкой", estimate.Evidence);
    }

    [Fact]
    public void Estimate_IgnoresSignalsWithoutStockOrDeliveryHours()
    {
        var estimate = WbDeliveryLocationEstimator.Estimate(
        [
            Signal("Москва", "Сегодня, склад WB", 5, quantity: 0),
            Signal("Санкт-Петербург", "Завтра, склад WB", 20, quantity: 48),
            Signal("Казань", "Послезавтра, склад WB", 41, quantity: 48),
            Signal("Екатеринбург", "17 июня, склад WB", 52, quantity: 48)
        ]);

        Assert.Equal("estimated", estimate.Status);
        Assert.Equal("north_west", estimate.ZoneKey);
        Assert.Equal("Северо-Запад", estimate.ZoneTitle);
        Assert.Equal("Санкт-Петербург", estimate.NearestDestinationName);
    }

    private static WbDeliveryLocationSignal Signal(
        string destinationName,
        string deliveryLabel,
        int? deliveryHours,
        int? quantity)
    {
        return new WbDeliveryLocationSignal(
            DestinationName: destinationName,
            DeliveryLabel: deliveryLabel,
            DeliveryHours: deliveryHours,
            TotalQuantityObserved: quantity,
            VisibleDeliveryStatus: deliveryHours.HasValue ? "calculated" : "empty");
    }
}
