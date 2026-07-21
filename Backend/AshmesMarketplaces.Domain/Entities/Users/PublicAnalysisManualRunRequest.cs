using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Users;

public sealed class PublicAnalysisManualRunRequest
{
    public const string QueuedStatus = "queued";
    public const string RunningStatus = "running";
    public const string CompletedStatus = "completed";
    public const string FailedStatus = "failed";
    public const string NoDataStatus = "no_data";

    public static readonly string[] ActiveStatuses = [QueuedStatus, RunningStatus];

    private PublicAnalysisManualRunRequest() { }

    public PublicAnalysisManualRunRequest(
        string scheduleKey,
        Guid requestedByUserId,
        DateTime requestedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(scheduleKey))
            throw new ArgumentException("Schedule key is required.", nameof(scheduleKey));
        if (requestedByUserId == Guid.Empty)
            throw new ArgumentException("Requested user id is required.", nameof(requestedByUserId));

        DateTimeUtc.EnsureUtc(requestedAtUtc, nameof(requestedAtUtc));

        Id = Guid.NewGuid();
        ScheduleKey = scheduleKey.Trim();
        Status = QueuedStatus;
        RequestedByUserId = requestedByUserId;
        RequestedAtUtc = requestedAtUtc;
        CreatedAtUtc = requestedAtUtc;
        UpdatedAtUtc = requestedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ScheduleKey { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public Guid RequestedByUserId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void MarkRunning(DateTime startedAtUtc)
    {
        DateTimeUtc.EnsureUtc(startedAtUtc, nameof(startedAtUtc));

        Status = RunningStatus;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = null;
        Error = null;
        UpdatedAtUtc = startedAtUtc;
    }

    public void MarkCompleted(DateTime completedAtUtc)
    {
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));

        Status = CompletedStatus;
        CompletedAtUtc = completedAtUtc;
        Error = null;
        UpdatedAtUtc = completedAtUtc;
    }

    public void MarkFailed(DateTime completedAtUtc, string error)
    {
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));

        Status = FailedStatus;
        CompletedAtUtc = completedAtUtc;
        Error = string.IsNullOrWhiteSpace(error) ? "Manual analysis run failed." : error.Trim();
        UpdatedAtUtc = completedAtUtc;
    }

    public void MarkNoData(DateTime completedAtUtc, string message)
    {
        DateTimeUtc.EnsureUtc(completedAtUtc, nameof(completedAtUtc));

        Status = NoDataStatus;
        CompletedAtUtc = completedAtUtc;
        Error = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        UpdatedAtUtc = completedAtUtc;
    }
}
