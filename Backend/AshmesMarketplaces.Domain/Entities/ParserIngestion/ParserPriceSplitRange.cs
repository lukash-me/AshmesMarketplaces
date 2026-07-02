using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserPriceSplitRange
{
    private ParserPriceSplitRange() { }

    public ParserPriceSplitRange(
        Guid jobId,
        Guid? parentRangeId,
        int minPriceU,
        int maxPriceU,
        int? expectedTotal,
        DateTime nowUtc)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("Job id is required.", nameof(jobId));
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        Id = Guid.NewGuid();
        JobId = jobId;
        ParentRangeId = parentRangeId;
        MinPriceU = Math.Max(0, minPriceU);
        MaxPriceU = Math.Max(MinPriceU, maxPriceU);
        ExpectedTotal = expectedTotal.HasValue ? Math.Max(0, expectedTotal.Value) : null;
        NextPage = 1;
        NextItemOffset = 0;
        Status = ParserPriceSplitRangeStatuses.Pending;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public Guid? ParentRangeId { get; private set; }
    public int MinPriceU { get; private set; }
    public int MaxPriceU { get; private set; }
    public int? ExpectedTotal { get; private set; }
    public int NextPage { get; private set; }
    public int NextItemOffset { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int AttemptsCount { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? CooldownUntilUtc { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Claim(DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        Status = ParserPriceSplitRangeStatuses.Processing;
        AttemptsCount += 1;
        LastAttemptAtUtc = nowUtc;
        CooldownUntilUtc = null;
        Error = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCompleted(int? expectedTotal, DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        ExpectedTotal = expectedTotal.HasValue ? Math.Max(0, expectedTotal.Value) : ExpectedTotal;
        Status = ParserPriceSplitRangeStatuses.Completed;
        Error = null;
        CooldownUntilUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkPending(int? expectedTotal, int nextPage, int nextItemOffset, DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        ExpectedTotal = expectedTotal.HasValue ? Math.Max(0, expectedTotal.Value) : ExpectedTotal;
        NextPage = Math.Max(1, nextPage);
        NextItemOffset = Math.Max(0, nextItemOffset);
        Status = ParserPriceSplitRangeStatuses.Pending;
        Error = null;
        CooldownUntilUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCompletedSplit(DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        Status = ParserPriceSplitRangeStatuses.CompletedSplit;
        Error = null;
        CooldownUntilUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCooldown(string? error, DateTime cooldownUntilUtc, DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(cooldownUntilUtc, nameof(cooldownUntilUtc));
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        Status = ParserPriceSplitRangeStatuses.Cooldown;
        CooldownUntilUtc = cooldownUntilUtc;
        Error = Normalize(error);
        UpdatedAtUtc = nowUtc;
    }

    public void MarkFailedRetryable(string? error, DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        Status = ParserPriceSplitRangeStatuses.FailedRetryable;
        Error = Normalize(error);
        UpdatedAtUtc = nowUtc;
    }

    public void MarkFailedFinal(string? error, DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        Status = ParserPriceSplitRangeStatuses.FailedFinal;
        Error = Normalize(error);
        UpdatedAtUtc = nowUtc;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
