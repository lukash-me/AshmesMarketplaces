namespace AshmesMarketplaces.Application.ParserObservability.Services;

public static class WbDeliveryLocationEstimator
{
    private const string StatusEstimated = "estimated";
    private const string StatusInsufficientData = "insufficient_data";
    private const string ConfidenceHigh = "high";
    private const string ConfidenceMedium = "medium";
    private const string ConfidenceLow = "low";

    public static WbDeliveryLocationEstimate Estimate(IReadOnlyList<WbDeliveryLocationSignal> signals)
    {
        var validSignals = signals
            .Where(IsValid)
            .OrderBy(x => x.DeliveryHours!.Value)
            .ThenBy(x => x.DestinationName, StringComparer.Ordinal)
            .ToList();

        if (validSignals.Count < 3)
        {
            return new WbDeliveryLocationEstimate(
                Status: StatusInsufficientData,
                ZoneKey: null,
                ZoneTitle: null,
                Confidence: null,
                NearestDestinationName: null,
                NearestDeliveryLabel: null,
                NearestDeliveryHours: null,
                SecondDestinationName: null,
                SecondDeliveryHours: null,
                FarthestDestinationName: null,
                DeliverySpreadHours: null,
                Evidence: ["Недостаточно контрольных точек с рассчитанной доставкой"]);
        }

        var nearest = validSignals[0];
        var second = validSignals[1];
        var farthest = validSignals[^1];
        var nearestHours = nearest.DeliveryHours!.Value;
        var secondHours = second.DeliveryHours!.Value;
        var farthestHours = farthest.DeliveryHours!.Value;
        var secondDelta = secondHours - nearestHours;
        var spread = farthestHours - nearestHours;
        var confidence = Confidence(secondDelta, spread);
        var zone = ResolveZone(nearest.DestinationName);
        var evidence = new List<string>
        {
            $"До {DestinationPrepositional(nearest.DestinationName)}: {nearest.DeliveryLabel ?? $"{nearestHours} ч"}",
            $"До {DestinationPrepositional(farthest.DestinationName)}: {farthest.DeliveryLabel ?? $"{farthestHours} ч"}",
            $"Разброс по точкам: {spread} ч"
        };

        if (confidence == ConfidenceLow && secondDelta < 6)
        {
            evidence.Add("Ближайшие точки отличаются меньше чем на 6 ч");
        }

        return new WbDeliveryLocationEstimate(
            Status: StatusEstimated,
            ZoneKey: zone.Key,
            ZoneTitle: zone.Title,
            Confidence: confidence,
            NearestDestinationName: nearest.DestinationName,
            NearestDeliveryLabel: nearest.DeliveryLabel,
            NearestDeliveryHours: nearestHours,
            SecondDestinationName: second.DestinationName,
            SecondDeliveryHours: secondHours,
            FarthestDestinationName: farthest.DestinationName,
            DeliverySpreadHours: spread,
            Evidence: evidence);
    }

    private static bool IsValid(WbDeliveryLocationSignal signal)
    {
        return string.Equals(signal.VisibleDeliveryStatus, "calculated", StringComparison.OrdinalIgnoreCase)
            && signal.TotalQuantityObserved > 0
            && signal.DeliveryHours > 0
            && !string.IsNullOrWhiteSpace(signal.DestinationName);
    }

    private static string Confidence(int secondDeltaHours, int spreadHours)
    {
        if (secondDeltaHours >= 12 || spreadHours >= 72)
            return ConfidenceHigh;

        if (secondDeltaHours >= 6)
            return ConfidenceMedium;

        return ConfidenceLow;
    }

    private static (string Key, string Title) ResolveZone(string destinationName)
    {
        return destinationName.Trim().ToLowerInvariant() switch
        {
            "москва" => ("moscow_central", "Москва / Центральный регион"),
            "санкт-петербург" => ("north_west", "Северо-Запад"),
            "казань" => ("volga", "Поволжье"),
            "екатеринбург" => ("ural", "Урал"),
            "новосибирск" => ("siberia", "Сибирь"),
            "краснодар" => ("south", "Юг"),
            "хабаровск" => ("far_east", "Дальний Восток"),
            _ => ("unknown", destinationName)
        };
    }

    private static string DestinationPrepositional(string destinationName)
    {
        return destinationName.Trim().ToLowerInvariant() switch
        {
            "москва" => "Москвы",
            "санкт-петербург" => "Санкт-Петербурга",
            "казань" => "Казани",
            "екатеринбург" => "Екатеринбурга",
            "новосибирск" => "Новосибирска",
            "краснодар" => "Краснодара",
            "хабаровск" => "Хабаровска",
            _ => destinationName
        };
    }
}

public sealed record WbDeliveryLocationSignal(
    string DestinationName,
    string? DeliveryLabel,
    int? DeliveryHours,
    int? TotalQuantityObserved,
    string? VisibleDeliveryStatus);

public sealed record WbDeliveryLocationEstimate(
    string Status,
    string? ZoneKey,
    string? ZoneTitle,
    string? Confidence,
    string? NearestDestinationName,
    string? NearestDeliveryLabel,
    int? NearestDeliveryHours,
    string? SecondDestinationName,
    int? SecondDeliveryHours,
    string? FarthestDestinationName,
    int? DeliverySpreadHours,
    IReadOnlyList<string> Evidence);
