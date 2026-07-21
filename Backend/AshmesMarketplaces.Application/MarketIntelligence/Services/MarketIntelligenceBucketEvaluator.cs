namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public static class MarketIntelligenceBucketEvaluator
{
    private const int ShortDescriptionLength = 80;

    public static QualityEvaluationResult EvaluateQuality(
        decimal? rating,
        int? feedbackCount,
        int? imageCount,
        string? description,
        int? characteristicsCount,
        bool hasProduct,
        bool hasDetails)
    {
        if (!hasProduct || !hasDetails)
        {
            return new QualityEvaluationResult(
                "unknown",
                ["Недостаточно данных карточки для оценки качества."]);
        }

        var reasons = new List<string>();
        var weakSignals = 0;

        if (rating.HasValue && rating.Value < 4.2m)
        {
            weakSignals++;
            reasons.Add($"Рейтинг ниже 4,2: {rating.Value:0.0}.");
        }

        if (feedbackCount.HasValue && feedbackCount.Value < 5)
        {
            weakSignals++;
            reasons.Add($"Мало отзывов: {feedbackCount.Value}.");
        }

        if (imageCount.HasValue && imageCount.Value <= 1)
        {
            weakSignals++;
            reasons.Add($"Мало фото: {imageCount.Value}.");
        }

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < ShortDescriptionLength)
        {
            weakSignals++;
            reasons.Add("Описание отсутствует или слишком короткое.");
        }

        if (characteristicsCount.HasValue && characteristicsCount.Value < 3)
        {
            weakSignals++;
            reasons.Add($"Мало характеристик: {characteristicsCount.Value}.");
        }

        if (weakSignals > 0)
            return new QualityEvaluationResult("weak", reasons);

        if (rating.HasValue
            && rating.Value >= 4.7m
            && feedbackCount.HasValue
            && feedbackCount.Value >= 30
            && imageCount.HasValue
            && imageCount.Value >= 3
            && !string.IsNullOrWhiteSpace(description)
            && description.Trim().Length >= ShortDescriptionLength
            && characteristicsCount.HasValue
            && characteristicsCount.Value >= 4)
        {
            return new QualityEvaluationResult(
                "strong",
                [
                    $"Сильные отзывы: рейтинг {rating.Value:0.0}, отзывов {feedbackCount.Value}.",
                    $"Карточка заполнена: фото {imageCount.Value}, характеристик {characteristicsCount.Value}."
                ]);
        }

        return new QualityEvaluationResult(
            "medium",
            ["Карточка без явных слабых признаков, но не дотягивает до сильной группы."]);
    }

    public static string EvaluateDelivery(DateTime? visibleDeliveryDate, DateTime? observedAtUtc)
    {
        if (!visibleDeliveryDate.HasValue || !observedAtUtc.HasValue)
            return "unknown";

        var days = (int)Math.Ceiling((visibleDeliveryDate.Value.Date - observedAtUtc.Value.Date).TotalDays);
        if (days <= 2)
            return "fast";
        if (days <= 6)
            return "medium";

        return "slow";
    }

    public static int CountCharacteristics(System.Text.Json.JsonElement? value)
    {
        if (!value.HasValue)
            return 0;

        var element = value.Value;
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Object => element.EnumerateObject().Count(),
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray().Count(),
            _ => 0
        };
    }
}

public sealed record QualityEvaluationResult(string Bucket, IReadOnlyList<string> Reasons);
