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
            .Select(x => x
                .OrderBy(item => item.Position ?? int.MaxValue)
                .ThenByDescending(item => item.FeedbackCount ?? 0)
                .First())
            .ToList();
        var sampleSize = sample.Count;
        var denominator = sampleSize == 0 ? 1 : sampleSize;

        var countRanking = BuildRanking(
            "count",
            "Топ по количеству",
            sample,
            denominator,
            RankingKind.Count);
        var positionRanking = BuildRanking(
            "position",
            "Топ по позиции",
            sample,
            denominator,
            RankingKind.Position);
        var reviewsRanking = BuildRanking(
            "reviews",
            "Топ по отзывам",
            sample,
            denominator,
            RankingKind.Reviews);
        var rankings = new[] { countRanking, positionRanking, reviewsRanking };

        var knownSellerSlots = countRanking.SellerLeaders.Sum(x => x.SlotsCount);
        var knownBrandSlots = countRanking.BrandLeaders.Sum(x => x.SlotsCount);
        var limitations = BuildLimitations(
            sampleSize,
            countRanking.SellerLeaders.Count,
            countRanking.BrandLeaders.Count,
            knownSellerSlots,
            knownBrandSlots);

        return new MarketConcentrationDto(
            sampleSize,
            countRanking.SellerLeaders.Count,
            countRanking.BrandLeaders.Count,
            countRanking.Top3SellersSharePercent,
            countRanking.Top5SellersSharePercent,
            countRanking.Hhi,
            countRanking.NormalizedConcentrationScore,
            countRanking.SellerLeaders,
            countRanking.BrandLeaders,
            countRanking.RootClusters,
            rankings,
            countRanking.Insight,
            limitations);
    }

    private static MarketConcentrationRankingDto BuildRanking(
        string key,
        string title,
        IReadOnlyList<MarketConcentrationInput> sample,
        int denominator,
        RankingKind rankingKind)
    {
        var totalFeedbackCount = sample.Sum(x => Math.Max(0, x.FeedbackCount ?? 0));
        var sellerGroups = BuildGroups(sample, x => x.SellerName, rankingKind, totalFeedbackCount);
        var brandGroups = BuildGroups(sample, x => x.BrandName, rankingKind, totalFeedbackCount);
        var rootClusters = BuildRootClusters(sample, denominator, rankingKind, totalFeedbackCount);
        var top3SellersShare = SharePercent(sellerGroups.Take(3).Sum(x => x.SlotsCount), denominator);
        var top5SellersShare = SharePercent(sellerGroups.Take(5).Sum(x => x.SlotsCount), denominator);
        var hhi = CalculateHhi(sellerGroups, denominator);
        var score = Math.Round(Math.Min(100m, Math.Max(top5SellersShare, hhi * 100m)), 1, MidpointRounding.AwayFromZero);

        return new MarketConcentrationRankingDto(
            key,
            title,
            sample.Count,
            top3SellersShare,
            top5SellersShare,
            hhi,
            score,
            sellerGroups.Take(MaxLeaders).ToList(),
            brandGroups.Take(MaxLeaders).ToList(),
            rootClusters.Take(MaxLeaders).ToList(),
            BuildInsight(rankingKind, top5SellersShare, rootClusters.Count, sample.Count, sellerGroups.Sum(x => x.SlotsCount)));
    }

    private static List<MarketConcentrationLeaderDto> BuildGroups(
        IReadOnlyList<MarketConcentrationInput> sample,
        Func<MarketConcentrationInput, string?> selector,
        RankingKind rankingKind,
        int totalFeedbackCount)
    {
        var denominator = sample.Count == 0 ? 1 : sample.Count;
        var groups = sample
            .Select(x => new
            {
                Name = Normalize(selector(x)),
                x.Position,
                FeedbackCount = Math.Max(0, x.FeedbackCount ?? 0)
            })
            .Where(x => x.Name is not null)
            .GroupBy(x => x.Name!, StringComparer.OrdinalIgnoreCase)
            .Select(x => new MarketConcentrationLeaderDto(
                x.Key,
                x.Count(),
                SharePercent(x.Count(), denominator),
                MinKnownPosition(x.Select(item => item.Position)),
                x.Sum(item => item.FeedbackCount),
                x.Count(item => item.Position.GetValueOrDefault() > 0),
                Top100SharePercent(x.Count(item => IsTop100(item.Position))),
                SharePercent(x.Sum(item => item.FeedbackCount), totalFeedbackCount)))
            .ToList();

        return SortLeaders(groups, rankingKind).ToList();
    }

    private static List<MarketConcentrationRootClusterDto> BuildRootClusters(
        IReadOnlyList<MarketConcentrationInput> sample,
        int denominator,
        RankingKind rankingKind,
        int totalFeedbackCount)
    {
        var groups = sample
            .Where(x => !string.IsNullOrWhiteSpace(x.WbRootId))
            .GroupBy(x => x.WbRootId!, StringComparer.Ordinal)
            .Select(x =>
            {
                var distinctProducts = x
                    .Select(item => item.WbProductId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                return new MarketConcentrationRootClusterDto(
                    x.Key,
                    distinctProducts,
                    MinKnownPosition(x.Select(item => item.Position)),
                    SharePercent(distinctProducts, denominator),
                    x.Sum(item => Math.Max(0, item.FeedbackCount ?? 0)),
                    x.Count(item => item.Position.GetValueOrDefault() > 0),
                    Top100SharePercent(x
                        .Where(item => !string.IsNullOrWhiteSpace(item.WbProductId))
                        .GroupBy(item => item.WbProductId!, StringComparer.Ordinal)
                        .Count(group => group.Any(item => IsTop100(item.Position)))),
                    SharePercent(x.Sum(item => Math.Max(0, item.FeedbackCount ?? 0)), totalFeedbackCount));
            })
            .Where(x => x.ProductCount > 1)
            .ToList();

        return SortRootClusters(groups, rankingKind).ToList();
    }

    private static IEnumerable<MarketConcentrationLeaderDto> SortLeaders(
        IReadOnlyList<MarketConcentrationLeaderDto> groups,
        RankingKind rankingKind)
    {
        return rankingKind switch
        {
            RankingKind.Position => groups
                .Where(x => x.BestPosition.HasValue)
                .OrderByDescending(x => x.Top100SharePercent)
                .ThenBy(x => x.BestPosition!.Value)
                .ThenByDescending(x => x.RankedSlotsCount)
                .ThenByDescending(x => x.SlotsCount)
                .ThenBy(x => x.Name),
            RankingKind.Reviews => groups
                .OrderByDescending(x => x.FeedbackCount)
                .ThenBy(x => x.BestPosition ?? int.MaxValue)
                .ThenByDescending(x => x.SlotsCount)
                .ThenBy(x => x.Name),
            _ => groups
                .OrderByDescending(x => x.SlotsCount)
                .ThenBy(x => x.BestPosition ?? int.MaxValue)
                .ThenByDescending(x => x.FeedbackCount)
                .ThenBy(x => x.Name)
        };
    }

    private static IEnumerable<MarketConcentrationRootClusterDto> SortRootClusters(
        IReadOnlyList<MarketConcentrationRootClusterDto> groups,
        RankingKind rankingKind)
    {
        return rankingKind switch
        {
            RankingKind.Position => groups
                .Where(x => x.BestPosition.HasValue)
                .OrderByDescending(x => x.Top100SharePercent)
                .ThenBy(x => x.BestPosition!.Value)
                .ThenByDescending(x => x.RankedSlotsCount)
                .ThenByDescending(x => x.ProductCount)
                .ThenBy(x => x.WbRootId),
            RankingKind.Reviews => groups
                .OrderByDescending(x => x.FeedbackCount)
                .ThenBy(x => x.BestPosition ?? int.MaxValue)
                .ThenByDescending(x => x.ProductCount)
                .ThenBy(x => x.WbRootId),
            _ => groups
                .OrderByDescending(x => x.ProductCount)
                .ThenBy(x => x.BestPosition ?? int.MaxValue)
                .ThenByDescending(x => x.FeedbackCount)
                .ThenBy(x => x.WbRootId)
        };
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
            limitations.Add("Нет товаров в выбранной нише для расчета концентрации.");
        if (sampleSize > 0 && sellersCount == 0)
            limitations.Add("В выбранной нише нет данных о продавцах.");
        if (sampleSize > 0 && brandsCount == 0)
            limitations.Add("В выбранной нише нет данных о брендах.");
        if (sampleSize > 0 && knownSellerSlots * 2 < sampleSize)
            limitations.Add("Для значительной части товаров ниши нет данных о продавцах; вывод по концентрации нужно читать как предварительный.");
        if (sampleSize > 0 && knownBrandSlots * 2 < sampleSize)
            limitations.Add("Для значительной части товаров ниши нет данных о брендах.");

        return limitations;
    }

    private static string BuildInsight(
        RankingKind rankingKind,
        decimal top5SellersSharePercent,
        int rootClusterCount,
        int sampleSize,
        int knownSellerSlots)
    {
        var hasLowSellerCoverage = sampleSize > 0 && knownSellerSlots * 2 < sampleSize;
        var baseInsight = rankingKind switch
        {
            RankingKind.Position => "Топ по позиции показывает игроков, которые реально видны выше в поисковой выдаче выбранной ниши.",
            RankingKind.Reviews => "Топ по отзывам показывает продавцов и бренды с наибольшим накопленным социальным доказательством.",
            _ when hasLowSellerCoverage => "Данных о продавцах недостаточно для уверенного вывода. Видимая часть рынка не показывает высокой концентрации, но часть товаров ниши пока без seller-данных.",
            _ => top5SellersSharePercent switch
            {
                < 25m => "Рынок фрагментирован. Вход потенциально проще: top-5 продавцов не удерживают значительную часть ниши.",
                <= 45m => "Рынок умеренно концентрирован. Перед входом стоит сравнить условия и оформление карточек лидеров.",
                _ => "Рынок концентрирован. Вход сложнее: несколько продавцов удерживают значительную часть ниши."
            }
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

    private static decimal Top100SharePercent(int count) =>
        SharePercent(count, 100);

    private static bool IsTop100(int? position) =>
        position is >= 1 and <= 100;

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static int? MinKnownPosition(IEnumerable<int?> values)
    {
        return values
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty()
            .Min() is var min && min > 0
                ? min
                : null;
    }

    private enum RankingKind
    {
        Count,
        Position,
        Reviews
    }
}

public sealed record MarketConcentrationInput(
    string WbProductId,
    string? WbRootId,
    string? SellerName,
    string? BrandName,
    int? Position,
    int? FeedbackCount);
