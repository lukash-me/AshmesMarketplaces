using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public static class MarketIntelligenceConcentrationCalculator
{
    private const int MaxLeaders = 10;

    public static MarketConcentrationDto Build(IReadOnlyList<MarketConcentrationInput> products)
    {
        var sample = products
            .Where(x => !string.IsNullOrWhiteSpace(x.WbProductId))
            .GroupBy(x => x.WbProductId!, StringComparer.Ordinal)
            .Select(x => x.OrderBy(item => item.Position).First())
            .ToList();
        var sampleSize = sample.Count;
        var denominator = sampleSize == 0 ? 1 : sampleSize;

        var sellerGroups = BuildGroups(sample, x => x.SellerName);
        var brandGroups = BuildGroups(sample, x => x.BrandName);
        var knownSellerSlots = sellerGroups.Sum(x => x.SlotsCount);
        var knownBrandSlots = brandGroups.Sum(x => x.SlotsCount);
        var rootClusters = BuildRootClusters(sample, denominator);
        var top3SellersShare = SharePercent(sellerGroups.Take(3).Sum(x => x.SlotsCount), denominator);
        var top5SellersShare = SharePercent(sellerGroups.Take(5).Sum(x => x.SlotsCount), denominator);
        var hhi = CalculateHhi(sellerGroups, denominator);
        var score = Math.Round(Math.Min(100m, Math.Max(top5SellersShare, hhi * 100m)), 1, MidpointRounding.AwayFromZero);
        var limitations = BuildLimitations(sampleSize, sellerGroups.Count, brandGroups.Count, knownSellerSlots, knownBrandSlots);
        var insight = BuildInsight(top5SellersShare, rootClusters.Count, sampleSize, knownSellerSlots);

        return new MarketConcentrationDto(
            sampleSize,
            sellerGroups.Count,
            brandGroups.Count,
            top3SellersShare,
            top5SellersShare,
            hhi,
            score,
            sellerGroups.Take(MaxLeaders).ToList(),
            brandGroups.Take(MaxLeaders).ToList(),
            rootClusters.Take(MaxLeaders).ToList(),
            insight,
            limitations);
    }

    private static List<MarketConcentrationLeaderDto> BuildGroups(
        IReadOnlyList<MarketConcentrationInput> sample,
        Func<MarketConcentrationInput, string?> selector)
    {
        return sample
            .Select(x => new
            {
                Name = Normalize(selector(x)),
                x.Position
            })
            .Where(x => x.Name is not null)
            .GroupBy(x => x.Name!, StringComparer.OrdinalIgnoreCase)
            .Select(x => new MarketConcentrationLeaderDto(
                x.Key,
                x.Count(),
                SharePercent(x.Count(), sample.Count == 0 ? 1 : sample.Count),
                x.Min(item => item.Position)))
            .OrderByDescending(x => x.SlotsCount)
            .ThenBy(x => x.BestPosition)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static List<MarketConcentrationRootClusterDto> BuildRootClusters(
        IReadOnlyList<MarketConcentrationInput> sample,
        int denominator)
    {
        return sample
            .Where(x => !string.IsNullOrWhiteSpace(x.WbRootId))
            .GroupBy(x => x.WbRootId!, StringComparer.Ordinal)
            .Select(x => new MarketConcentrationRootClusterDto(
                x.Key,
                x.Select(item => item.WbProductId).Distinct(StringComparer.Ordinal).Count(),
                x.Min(item => item.Position),
                SharePercent(x.Select(item => item.WbProductId).Distinct(StringComparer.Ordinal).Count(), denominator)))
            .Where(x => x.ProductCount > 1)
            .OrderByDescending(x => x.ProductCount)
            .ThenBy(x => x.BestPosition)
            .ThenBy(x => x.WbRootId)
            .ToList();
    }

    private static decimal CalculateHhi(IReadOnlyList<MarketConcentrationLeaderDto> groups, int denominator)
    {
        if (denominator <= 0 || groups.Count == 0)
            return 0m;

        var hhi = groups
            .Select(x => (decimal)x.SlotsCount / denominator)
            .Sum(share => share * share);
        return Math.Round(hhi, 4, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<string> BuildLimitations(
        int sampleSize,
        int sellersCount,
        int brandsCount,
        int knownSellerSlots,
        int knownBrandSlots)
    {
        var limitations = new List<string>();
        if (sampleSize == 0)
            limitations.Add("Нет товаров в выбранном topN для расчета концентрации.");
        if (sampleSize > 0 && sellersCount == 0)
            limitations.Add("В выбранном topN нет данных о продавцах.");
        if (sampleSize > 0 && brandsCount == 0)
            limitations.Add("В выбранном topN нет данных о брендах.");
        if (sampleSize > 0 && knownSellerSlots * 2 < sampleSize)
            limitations.Add("Для значительной части topN нет данных о продавцах; вывод по концентрации нужно читать как предварительный.");
        if (sampleSize > 0 && knownBrandSlots * 2 < sampleSize)
            limitations.Add("Для значительной части topN нет данных о брендах.");

        return limitations;
    }

    private static string BuildInsight(
        decimal top5SellersSharePercent,
        int rootClusterCount,
        int sampleSize,
        int knownSellerSlots)
    {
        var hasLowSellerCoverage = sampleSize > 0 && knownSellerSlots * 2 < sampleSize;
        var baseInsight = hasLowSellerCoverage
            ? "Данных о продавцах недостаточно для уверенного вывода. Видимая часть рынка не показывает высокой концентрации, но часть topN пока без seller-данных."
            : top5SellersSharePercent switch
            {
                < 25m => "Рынок фрагментирован. Вход потенциально проще: top-5 продавцов не удерживают значительную часть выдачи.",
                <= 45m => "Рынок умеренно концентрирован. Перед входом стоит сравнить условия и оформление карточек лидеров.",
                _ => "Рынок концентрирован. Вход сложнее: несколько продавцов удерживают значительную часть топа."
            };

        if (rootClusterCount >= 5)
            return $"{baseInsight} В нише много повторяющихся карточек. Отличие в продукте, фото или упаковке может быть важнее цены.";

        return baseInsight;
    }

    private static decimal SharePercent(int count, int total)
    {
        if (total <= 0)
            return 0m;

        return Math.Round(count * 100m / total, 1, MidpointRounding.AwayFromZero);
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

public sealed record MarketConcentrationInput(
    string WbProductId,
    string? WbRootId,
    string? SellerName,
    string? BrandName,
    int Position);
