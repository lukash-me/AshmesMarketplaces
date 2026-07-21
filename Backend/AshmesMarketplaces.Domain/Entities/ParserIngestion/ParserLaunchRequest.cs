using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserLaunchRequest
{
    private const int MaxErrorLength = 4000;

    private ParserLaunchRequest() { }

    public ParserLaunchRequest(
        Guid parserInstanceConfigurationId,
        string parserInstanceId,
        string launchMode,
        string? proxyKey,
        int? batchLimit,
        Guid requestedByUserId,
        DateTime requestedAtUtc)
    {
        if (parserInstanceConfigurationId == Guid.Empty)
            throw new ArgumentException("Parser instance configuration id is required.", nameof(parserInstanceConfigurationId));
        if (string.IsNullOrWhiteSpace(parserInstanceId))
            throw new ArgumentException("Parser instance id is required.", nameof(parserInstanceId));
        if (requestedByUserId == Guid.Empty)
            throw new ArgumentException("Requested user id is required.", nameof(requestedByUserId));
        DateTimeUtc.EnsureUtc(requestedAtUtc, nameof(requestedAtUtc));

        Id = Guid.NewGuid();
        ParserInstanceConfigurationId = parserInstanceConfigurationId;
        ParserInstanceId = parserInstanceId.Trim();
        ParserCycleId = $"parser-cycle-{Id:N}";
        LaunchMode = NormalizeLaunchMode(launchMode);
        ProxyKey = NormalizeOptional(proxyKey);
        BatchLimit = batchLimit;
        RequestedByUserId = requestedByUserId;
        Status = ParserLaunchRequestStatuses.Queued;
        RequestedAtUtc = requestedAtUtc;
        CreatedAtUtc = requestedAtUtc;
        UpdatedAtUtc = requestedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ParserInstanceConfigurationId { get; private set; }
    public string ParserInstanceId { get; private set; } = string.Empty;
    public string ParserCycleId { get; private set; } = string.Empty;
    public string LaunchMode { get; private set; } = string.Empty;
    public string? ProxyKey { get; private set; }
    public int? BatchLimit { get; private set; }
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
        EnsureUtc(startedAtUtc);
        Status = ParserLaunchRequestStatuses.Running;
        StartedAtUtc = startedAtUtc;
        Error = null;
        UpdatedAtUtc = startedAtUtc;
    }

    public void AssignParserCycle(string parserCycleId)
    {
        if (string.IsNullOrWhiteSpace(parserCycleId))
            throw new ArgumentException("Parser cycle id is required.", nameof(parserCycleId));

        ParserCycleId = parserCycleId.Trim();
    }

    public void MarkCompleted(DateTime completedAtUtc)
    {
        EnsureUtc(completedAtUtc);
        Status = ParserLaunchRequestStatuses.Completed;
        CompletedAtUtc = completedAtUtc;
        Error = null;
        UpdatedAtUtc = completedAtUtc;
    }

    public void MarkFailed(string? error, DateTime completedAtUtc)
    {
        EnsureUtc(completedAtUtc);
        Status = ParserLaunchRequestStatuses.Failed;
        CompletedAtUtc = completedAtUtc;
        Error = NormalizeError(error);
        UpdatedAtUtc = completedAtUtc;
    }

    public void Cancel(string? reason, DateTime completedAtUtc)
    {
        EnsureUtc(completedAtUtc);
        Status = ParserLaunchRequestStatuses.Cancelled;
        CompletedAtUtc = completedAtUtc;
        Error = NormalizeError(reason ?? "Parser launch was cancelled.");
        UpdatedAtUtc = completedAtUtc;
    }

    private static string NormalizeError(string? error)
    {
        var normalized = string.IsNullOrWhiteSpace(error) ? "Parser launch failed." : error.Trim();
        return normalized.Length <= MaxErrorLength ? normalized : normalized[..MaxErrorLength];
    }

    private static string NormalizeLaunchMode(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is ParserLaunchModes.LimitedAll or ParserLaunchModes.FullAll or ParserLaunchModes.CheckProxy
            ? normalized
            : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void EnsureUtc(DateTime value)
    {
        DateTimeUtc.EnsureUtc(value, nameof(value));
    }
}

public static class ParserLaunchModes
{
    public const string LimitedAll = "limited_all";
    public const string FullAll = "full_all";
    public const string CheckProxy = "check_proxy";
}

public static class ParserLaunchRequestStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    public static readonly string[] Active = [Queued, Running];
}
