using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public static class MarketIntelligencePriceCorridorCalculator
{
    public static PriceCorridorsDto Build(IReadOnlyList<PriceCorridorInput> products)
    {
        var prices = products
            .Where(x => x.Price.HasValue)
            .Select(x => x.Price!.Value)
            .OrderBy(x => x)
            .ToList();

        if (prices.Count == 0)
        {
            return new PriceCorridorsDto(
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                "Недостаточно данных по ценам для построения коридоров ниши.",
                ["Нет товаров с валидной текущей ценой."]);
        }

        var min = prices[0];
        var p25 = Percentile(prices, 0.25m);
        var median = Percentile(prices, 0.5m);
        var p75 = Percentile(prices, 0.75m);
        var p90 = Percentile(prices, 0.9m);
        var max = prices[^1];
        var average = Math.Round(prices.Average(), 2);

        var lowerCount = products.Count(x => x.Price.HasValue && x.Price.Value <= p25);
        var massCount = products.Count(x => x.Price.HasValue && x.Price.Value > p25 && x.Price.Value <= p75);
        var premiumCount = products.Count(x => x.Price.HasValue && x.Price.Value > p75);

        var segments = new List<PriceCorridorSegmentDto>
        {
            new(
                "lower",
                "Нижний сегмент",
                min,
                p25,
                lowerCount,
                Percent(lowerCount, prices.Count),
                MedianRating(products.Where(x => x.Price.HasValue && x.Price.Value <= p25))),
            new(
                "mass",
                "Массовый сегмент",
                p25,
                p75,
                massCount,
                Percent(massCount, prices.Count),
                MedianRating(products.Where(x => x.Price.HasValue && x.Price.Value > p25 && x.Price.Value <= p75))),
            new(
                "premium",
                "Премиум",
                p75,
                max,
                premiumCount,
                Percent(premiumCount, prices.Count),
                MedianRating(products.Where(x => x.Price.HasValue && x.Price.Value > p75)))
        };

        var top10Median = MedianByPosition(products, 10);
        var top50Median = MedianByPosition(products, 50);
        var top100Median = MedianByPosition(products, 100);

        return new PriceCorridorsDto(
            prices.Count,
            min,
            p25,
            median,
            p75,
            p90,
            max,
            average,
            top10Median,
            top50Median,
            top100Median,
            segments,
            BuildInsight(p25, p75, median, p90, top50Median),
            []);
    }

    private static decimal? MedianByPosition(IEnumerable<PriceCorridorInput> products, int maxPosition)
    {
        var prices = products
            .Where(x => x.Price.HasValue && x.Position.HasValue && x.Position.Value <= maxPosition)
            .Select(x => x.Price!.Value)
            .OrderBy(x => x)
            .ToList();

        return prices.Count == 0 ? null : Percentile(prices, 0.5m);
    }

    private static decimal? MedianRating(IEnumerable<PriceCorridorInput> products)
    {
        var ratings = products
            .Where(x => x.Rating.HasValue && x.Rating.Value > 0)
            .Select(x => x.Rating!.Value)
            .OrderBy(x => x)
            .ToList();

        return ratings.Count == 0 ? null : Percentile(ratings, 0.5m);
    }

    private static decimal Percentile(IReadOnlyList<decimal> sortedValues, decimal percentile)
    {
        if (sortedValues.Count == 1)
            return sortedValues[0];

        var position = (sortedValues.Count - 1) * percentile;
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
            return sortedValues[lowerIndex];

        var weight = position - lowerIndex;
        return Math.Round(sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight, 2);
    }

    private static decimal Percent(int count, int total)
    {
        return total <= 0 ? 0 : Math.Round(count * 100m / total, 2);
    }

    private static string BuildInsight(decimal p25, decimal p75, decimal median, decimal p90, decimal? top50Median)
    {
        var parts = new List<string>
        {
            $"Основной рынок находится в диапазоне {FormatMoney(p25)}–{FormatMoney(p75)}."
        };

        if (top50Median.HasValue)
        {
            var direction = top50Median.Value > median ? "дороже" : top50Median.Value < median ? "дешевле" : "на уровне";
            parts.Add($"Топ-50 выдачи {direction} общей медианы: {FormatMoney(top50Median.Value)} против {FormatMoney(median)}.");
        }

        if (p90 > p75 * 1.35m)
            parts.Add("В нише есть выраженный премиум-хвост: верхний сегмент требует сильной карточки, отзывов и понятного преимущества.");

        return string.Join(" ", parts);
    }

    private static string FormatMoney(decimal value)
    {
        return $"{Math.Round(value, 0):N0} ₽".Replace(",", " ");
    }
}

public sealed record PriceCorridorInput(decimal? Price, int? Position, decimal? Rating = null);
