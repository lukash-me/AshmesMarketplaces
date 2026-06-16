using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Users;

public sealed class UserAnalysisSchedule
{
    public const string PendingStatus = "pending";
    public const string RunningStatus = "running";
    public const string CompletedStatus = "completed";
    public const string FailedStatus = "failed";

    private UserAnalysisSchedule() { }

    public UserAnalysisSchedule(
        Guid idUser,
        string timezoneId,
        TimeOnly hotProductsLocalTime,
        TimeOnly overviewLocalTime,
        DateTime nextHotProductsRunAtUtc,
        DateTime nextOverviewRunAtUtc,
        DateTime createdAtUtc)
    {
        if (idUser == Guid.Empty)
            throw new ArgumentException("User id is required.", nameof(idUser));

        if (string.IsNullOrWhiteSpace(timezoneId))
            throw new ArgumentException("Timezone id is required.", nameof(timezoneId));

        DateTimeUtc.EnsureUtc(nextHotProductsRunAtUtc, nameof(nextHotProductsRunAtUtc));
        DateTimeUtc.EnsureUtc(nextOverviewRunAtUtc, nameof(nextOverviewRunAtUtc));
        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        IdUser = idUser;
        TimezoneId = timezoneId;
        HotProductsLocalTime = hotProductsLocalTime;
        OverviewLocalTime = overviewLocalTime;
        NextHotProductsRunAtUtc = nextHotProductsRunAtUtc;
        NextOverviewRunAtUtc = nextOverviewRunAtUtc;
        LastHotProductsStatus = PendingStatus;
        LastOverviewStatus = PendingStatus;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid IdUser { get; private set; }
    public string TimezoneId { get; private set; } = string.Empty;
    public TimeOnly HotProductsLocalTime { get; private set; }
    public TimeOnly OverviewLocalTime { get; private set; }
    public DateTime NextHotProductsRunAtUtc { get; private set; }
    public DateTime NextOverviewRunAtUtc { get; private set; }
    public DateTime? LastHotProductsStartedAtUtc { get; private set; }
    public DateTime? LastHotProductsCompletedAtUtc { get; private set; }
    public string? LastHotProductsStatus { get; private set; }
    public string? LastHotProductsError { get; private set; }
    public DateTime? LastOverviewStartedAtUtc { get; private set; }
    public DateTime? LastOverviewCompletedAtUtc { get; private set; }
    public string? LastOverviewStatus { get; private set; }
    public string? LastOverviewError { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public string? LockedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public bool IsLocked(DateTime nowUtc) => LockedUntilUtc.HasValue && LockedUntilUtc.Value > nowUtc;

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

    public void MarkHotProductsRunning(DateTime startedAtUtc)
    {
        DateTimeUtc.EnsureUtc(startedAtUtc, nameof(startedAtUtc));

        LastHotProductsStartedAtUtc = startedAtUtc;
        LastHotProductsCompletedAtUtc = null;
        LastHotProductsStatus = RunningStatus;
        LastHotProductsError = null;
        UpdatedAtUtc = startedAtUtc;
    }

    public void MarkHotProductsCompleted(DateTime completedAtUtc, DateTime nextRunAtUtc)
    {
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        DateTimeUtc.EnsureUtc(nextRunAtUtc, nameof(nextRunAtUtc));

        LastHotProductsCompletedAtUtc = completedAtUtc;
        LastHotProductsStatus = CompletedStatus;
        LastHotProductsError = null;
        NextHotProductsRunAtUtc = nextRunAtUtc;
        UpdatedAtUtc = completedAtUtc;
    }

    public void MarkHotProductsFailed(DateTime completedAtUtc, DateTime retryAtUtc, string error)
    {
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        DateTimeUtc.EnsureUtc(retryAtUtc, nameof(retryAtUtc));

        LastHotProductsCompletedAtUtc = completedAtUtc;
        LastHotProductsStatus = FailedStatus;
        LastHotProductsError = error;
        NextHotProductsRunAtUtc = retryAtUtc;
        UpdatedAtUtc = completedAtUtc;
    }

    public void MarkOverviewRunning(DateTime startedAtUtc)
    {
        DateTimeUtc.EnsureUtc(startedAtUtc, nameof(startedAtUtc));

        LastOverviewStartedAtUtc = startedAtUtc;
        LastOverviewCompletedAtUtc = null;
        LastOverviewStatus = RunningStatus;
        LastOverviewError = null;
        UpdatedAtUtc = startedAtUtc;
    }

    public void MarkOverviewCompleted(DateTime completedAtUtc, DateTime nextRunAtUtc)
    {
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        DateTimeUtc.EnsureUtc(nextRunAtUtc, nameof(nextRunAtUtc));

        LastOverviewCompletedAtUtc = completedAtUtc;
        LastOverviewStatus = CompletedStatus;
        LastOverviewError = null;
        NextOverviewRunAtUtc = nextRunAtUtc;
        UpdatedAtUtc = completedAtUtc;
    }

    public void MarkOverviewFailed(DateTime completedAtUtc, DateTime retryAtUtc, string error)
    {
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        DateTimeUtc.EnsureUtc(retryAtUtc, nameof(retryAtUtc));

        LastOverviewCompletedAtUtc = completedAtUtc;
        LastOverviewStatus = FailedStatus;
        LastOverviewError = error;
        NextOverviewRunAtUtc = retryAtUtc;
        UpdatedAtUtc = completedAtUtc;
    }
}
