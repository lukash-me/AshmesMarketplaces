using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Users;

public sealed class PublicAnalysisSchedule
{
    public const string HotProductsScheduleKey = "hot_products_public";
    public const string ParserCurrentProductsScheduleKey = "parser_current_products_public";
    public const string MarketIntelligenceScheduleKey = "market_intelligence_public";
    public const string MarketLogisticsEventsScheduleKey = "market_logistics_events_public";
    public const string ProductAvailabilityScheduleKey = "product_availability_public";
    public const string MarketConcentrationScheduleKey = "market_concentration_public";
    public const string TopForecastScheduleKey = "top_forecast_public";
    public const string WildberriesCategoryCatalogScheduleKey = "wb_category_catalog_refresh";
    public const string PendingStatus = "pending";
    public const string RunningStatus = "running";
    public const string CompletedStatus = "completed";
    public const string FailedStatus = "failed";

    private PublicAnalysisSchedule() { }

    public PublicAnalysisSchedule(
        string scheduleKey,
        string timezoneId,
        TimeOnly localTime,
        DateTime nextRunAtUtc,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(scheduleKey))
            throw new ArgumentException("Schedule key is required.", nameof(scheduleKey));

        if (string.IsNullOrWhiteSpace(timezoneId))
            throw new ArgumentException("Timezone id is required.", nameof(timezoneId));

        DateTimeUtc.EnsureUtc(nextRunAtUtc, nameof(nextRunAtUtc));
        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        ScheduleKey = scheduleKey.Trim();
        TimezoneId = timezoneId.Trim();
        LocalTime = localTime;
        NextRunAtUtc = nextRunAtUtc;
        LastStatus = PendingStatus;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string ScheduleKey { get; private set; } = string.Empty;
    public string TimezoneId { get; private set; } = string.Empty;
    public TimeOnly LocalTime { get; private set; }
    public DateTime NextRunAtUtc { get; private set; }
    public DateTime? LastStartedAtUtc { get; private set; }
    public DateTime? LastCompletedAtUtc { get; private set; }
    public string? LastStatus { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public string? LockedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Lock(string workerId, DateTime lockedUntilUtc, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker id is required.", nameof(workerId));

        DateTimeUtc.EnsureUtc(lockedUntilUtc, nameof(lockedUntilUtc));
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        LockedBy = workerId;
        LockedUntilUtc = lockedUntilUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void ReleaseLock(DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        LockedBy = null;
        LockedUntilUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkRunning(DateTime startedAtUtc)
    {
        DateTimeUtc.EnsureUtc(startedAtUtc, nameof(startedAtUtc));

        LastStartedAtUtc = startedAtUtc;
        LastCompletedAtUtc = null;
        LastStatus = RunningStatus;
        LastError = null;
        UpdatedAtUtc = startedAtUtc;
    }

    public void MarkCompleted(DateTime completedAtUtc, DateTime nextRunAtUtc)
    {
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        DateTimeUtc.EnsureUtc(nextRunAtUtc, nameof(nextRunAtUtc));

        LastCompletedAtUtc = completedAtUtc;
        LastStatus = CompletedStatus;
        LastError = null;
        NextRunAtUtc = nextRunAtUtc;
        UpdatedAtUtc = completedAtUtc;
    }

    public void MarkFailed(DateTime completedAtUtc, DateTime retryAtUtc, string error)
    {
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        DateTimeUtc.EnsureUtc(retryAtUtc, nameof(retryAtUtc));

        LastCompletedAtUtc = completedAtUtc;
        LastStatus = FailedStatus;
        LastError = error;
        NextRunAtUtc = retryAtUtc;
        UpdatedAtUtc = completedAtUtc;
    }

    public void RequestRun(DateTime requestedAtUtc)
    {
        DateTimeUtc.EnsureUtc(requestedAtUtc, nameof(requestedAtUtc));

        NextRunAtUtc = requestedAtUtc;
        LastStatus = PendingStatus;
        LastError = null;
        LockedBy = null;
        LockedUntilUtc = null;
        UpdatedAtUtc = requestedAtUtc;
    }
}
