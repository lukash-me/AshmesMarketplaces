using System.Text.Json;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

internal static class WbVisibleDeliveryCalculator
{
    private const string StatusEmpty = "empty";
    private const string StatusCalculated = "calculated";
    private const string SourceTime1PlusTime2 = "wb_time1_plus_time2_calculated";
    private const string CalculationVersion = "wb_time1_plus_time2_v1";
    private const long WbWarehouseDtypeFlag = 8;

    private static readonly TimeZoneInfo MoscowTimeZone = ResolveMoscowTimeZone();
    private static readonly string[] MonthNames =
    [
        "января",
        "февраля",
        "марта",
        "апреля",
        "мая",
        "июня",
        "июля",
        "августа",
        "сентября",
        "октября",
        "ноября",
        "декабря"
    ];

    public static WbVisibleDeliveryCalculation Calculate(
        int? productTime1Raw,
        int? productTime2Raw,
        long? productDtypeRaw,
        int? totalQuantityObserved,
        DateTime observedAtUtc)
    {
        if (totalQuantityObserved == 0)
        {
            return Empty("out_of_stock", productTime1Raw, productTime2Raw, productDtypeRaw, totalQuantityObserved);
        }

        if (!productTime1Raw.HasValue || !productTime2Raw.HasValue)
        {
            return Empty("missing_time1_or_time2", productTime1Raw, productTime2Raw, productDtypeRaw, totalQuantityObserved);
        }

        var deliveryHours = productTime1Raw.Value + productTime2Raw.Value;
        if (deliveryHours <= 0)
        {
            return Empty(
                "non_positive_delivery_hours",
                productTime1Raw,
                productTime2Raw,
                productDtypeRaw,
                totalQuantityObserved,
                deliveryHours);
        }

        var observedUtc = EnsureUtc(observedAtUtc);
        var deliveryDate = CalculateDeliveryDate(observedUtc, deliveryHours);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(observedUtc, MoscowTimeZone));
        var warehouseLabel = IsWbWarehouse(productDtypeRaw) ? "склад WB" : "склад продавца";
        var label = $"{FormatDeliveryDay(deliveryDate, today)}, {warehouseLabel}";
        var dateUtc = DateTime.SpecifyKind(deliveryDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        return new WbVisibleDeliveryCalculation(
            StatusCalculated,
            label,
            dateUtc,
            SourceTime1PlusTime2,
            observedUtc,
            JsonSerializer.SerializeToElement(new
            {
                deliveryHours,
                calculationVersion = CalculationVersion,
                productTime1 = productTime1Raw,
                productTime2 = productTime2Raw,
                productDtype = productDtypeRaw,
                totalQuantity = totalQuantityObserved,
                warehouseLabel,
                calculation = "delivery_hours=product_time1+product_time2; formatted_like_wb_deliveryDateTxt"
            }));
    }

    private static WbVisibleDeliveryCalculation Empty(
        string reason,
        int? productTime1Raw,
        int? productTime2Raw,
        long? productDtypeRaw,
        int? totalQuantityObserved,
        int? deliveryHours = null)
    {
        return new WbVisibleDeliveryCalculation(
            StatusEmpty,
            null,
            null,
            null,
            null,
            JsonSerializer.SerializeToElement(new
            {
                reason,
                deliveryHours,
                productTime1 = productTime1Raw,
                productTime2 = productTime2Raw,
                productDtype = productDtypeRaw,
                totalQuantity = totalQuantityObserved
            }));
    }

    private static DateOnly CalculateDeliveryDate(DateTime observedAtUtc, int deliveryHours)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(observedAtUtc, MoscowTimeZone);
        var arrival = now.AddHours(deliveryHours);
        if (arrival.Month == 1 && arrival.Day == 1)
        {
            arrival = arrival.AddDays(1);
            deliveryHours += 24;
        }

        var hoursUntilMidnight = 24 - now.Hour;
        var arrivalHour = arrival.Hour;
        var isDec31 = now.Month == 12 && now.Day == 31;
        var isJan1 = now.Month == 1 && now.Day == 1;

        if (now.Date == arrival.Date && deliveryHours < 24)
        {
            if (arrivalHour < 9)
                return DateOnly.FromDateTime(isJan1 ? now.AddDays(1) : now);

            if (arrivalHour < 23)
                return DateOnly.FromDateTime(isJan1 && deliveryHours < 5 ? now.AddDays(1) : now);

            return DateOnly.FromDateTime(now.AddDays(isDec31 ? 2 : 1));
        }

        if (deliveryHours < hoursUntilMidnight + 9)
            return DateOnly.FromDateTime(now.AddDays(isDec31 ? 2 : 1));

        if (deliveryHours < 24 + hoursUntilMidnight)
            return DateOnly.FromDateTime(now.AddDays(!isDec31 && arrivalHour < 23 ? 1 : 2));

        if (deliveryHours < 48 + hoursUntilMidnight && arrivalHour < 23)
            return DateOnly.FromDateTime(now.AddDays(2));

        if (arrivalHour >= 23)
            deliveryHours += hoursUntilMidnight;

        var deliveryDate = DateOnly.FromDateTime(now.AddHours(deliveryHours));
        if (deliveryDate.Month == 1 && deliveryDate.Day == 1)
            deliveryDate = deliveryDate.AddDays(1);

        return deliveryDate;
    }

    private static string FormatDeliveryDay(DateOnly deliveryDate, DateOnly today)
    {
        var deltaDays = deliveryDate.DayNumber - today.DayNumber;
        return deltaDays switch
        {
            0 => "Сегодня",
            1 => "Завтра",
            2 => "Послезавтра",
            _ => $"{deliveryDate.Day} {MonthNames[deliveryDate.Month - 1]}"
        };
    }

    private static bool IsWbWarehouse(long? dtype)
    {
        return dtype.HasValue && (dtype.Value & WbWarehouseDtypeFlag) != 0;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static TimeZoneInfo ResolveMoscowTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
        }
    }
}

internal sealed record WbVisibleDeliveryCalculation(
    string Status,
    string? Label,
    DateTime? Date,
    string? Source,
    DateTime? ObservedAtUtc,
    JsonElement RawPayload);
