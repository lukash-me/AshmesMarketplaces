using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserProxyRun
{
    private ParserProxyRun() { }

    public ParserProxyRun(
        string parserInstanceId,
        string externalProxyRunId,
        string proxyKey,
        string sourceCategory,
        string sourceSubcategory,
        int plannedProductsCount,
        int downloadedProductsCount,
        string? egressIp,
        string? tokenRef,
        string? sessionStatus,
        DateTime startedAtUtc,
        string? phase = null,
        int plannedRangesCount = 0,
        int completedRangesCount = 0,
        double rangeProgressPercent = 0)
        : this(
            parserInstanceId,
            externalProxyRunId,
            null,
            null,
            proxyKey,
            sourceCategory,
            sourceSubcategory,
            plannedProductsCount,
            downloadedProductsCount,
            egressIp,
            tokenRef,
            sessionStatus,
            startedAtUtc,
            phase,
            plannedRangesCount,
            completedRangesCount,
            rangeProgressPercent)
    {
    }

    public ParserProxyRun(
        string parserInstanceId,
        string externalProxyRunId,
        string? parserCycleId,
        string? cycleKind,
        string proxyKey,
        string sourceCategory,
        string sourceSubcategory,
        int plannedProductsCount,
        int downloadedProductsCount,
        string? egressIp,
        string? tokenRef,
        string? sessionStatus,
        DateTime startedAtUtc,
        string? phase = null,
        int plannedRangesCount = 0,
        int completedRangesCount = 0,
        double rangeProgressPercent = 0)
    {
        if (string.IsNullOrWhiteSpace(parserInstanceId))
            throw new ArgumentException("Parser instance id is required.", nameof(parserInstanceId));
        if (string.IsNullOrWhiteSpace(externalProxyRunId))
            throw new ArgumentException("External proxy run id is required.", nameof(externalProxyRunId));
        if (string.IsNullOrWhiteSpace(proxyKey))
            throw new ArgumentException("Proxy key is required.", nameof(proxyKey));
        if (string.IsNullOrWhiteSpace(sourceCategory))
            throw new ArgumentException("Source category is required.", nameof(sourceCategory));
        if (string.IsNullOrWhiteSpace(sourceSubcategory))
            throw new ArgumentException("Source subcategory is required.", nameof(sourceSubcategory));

        DateTimeUtc.EnsureUtc(startedAtUtc, nameof(startedAtUtc));

        Id = Guid.NewGuid();
        ParserInstanceId = parserInstanceId.Trim();
        ExternalProxyRunId = externalProxyRunId.Trim();
        ParserCycleId = string.IsNullOrWhiteSpace(parserCycleId)
            ? PipelineRunKey(externalProxyRunId)
            : parserCycleId.Trim();
        CycleKind = NormalizeCycleKind(cycleKind);
        ProxyKey = proxyKey.Trim();
        SourceCategory = sourceCategory.Trim();
        SourceSubcategory = sourceSubcategory.Trim();
        EgressIp = NormalizeOptional(egressIp);
        TokenRef = NormalizeOptional(tokenRef);
        SessionStatus = NormalizeOptional(sessionStatus);
        Status = ParserProxyRunStatuses.Running;
        Phase = NormalizePhase(phase);
        PlannedProductsCount = Math.Max(0, plannedProductsCount);
        DownloadedProductsCount = Math.Max(0, downloadedProductsCount);
        PlannedRangesCount = Math.Max(0, plannedRangesCount);
        CompletedRangesCount = Math.Max(0, completedRangesCount);
        RangeProgressPercent = ClampPercent(rangeProgressPercent);
        StartedAtUtc = startedAtUtc;
        LastHeartbeatAtUtc = startedAtUtc;
        CreatedAtUtc = startedAtUtc;
        UpdatedAtUtc = startedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ParserInstanceId { get; private set; } = string.Empty;
    public string ExternalProxyRunId { get; private set; } = string.Empty;
    public string ParserCycleId { get; private set; } = string.Empty;
    public string CycleKind { get; private set; } = ParserProxyRunCycleKinds.Legacy;
    public string ProxyKey { get; private set; } = string.Empty;
    public string SourceCategory { get; private set; } = string.Empty;
    public string SourceSubcategory { get; private set; } = string.Empty;
    public string? EgressIp { get; private set; }
    public string? TokenRef { get; private set; }
    public string? SessionStatus { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string Phase { get; private set; } = ParserProxyRunPhases.Download;
    public int PlannedProductsCount { get; private set; }
    public int DownloadedProductsCount { get; private set; }
    public int PlannedRangesCount { get; private set; }
    public int CompletedRangesCount { get; private set; }
    public double RangeProgressPercent { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime LastHeartbeatAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateProgress(
        int plannedProductsCount,
        int downloadedProductsCount,
        DateTime nowUtc,
        string? phase = null,
        int? plannedRangesCount = null,
        int? completedRangesCount = null,
        double? rangeProgressPercent = null)
    {
        EnsureUtc(nowUtc);
        PlannedProductsCount = Math.Max(PlannedProductsCount, plannedProductsCount);
        DownloadedProductsCount = Math.Max(DownloadedProductsCount, downloadedProductsCount);
        if (!string.IsNullOrWhiteSpace(phase))
            Phase = NormalizePhase(phase);
        if (plannedRangesCount.HasValue)
            PlannedRangesCount = Math.Max(PlannedRangesCount, plannedRangesCount.Value);
        if (completedRangesCount.HasValue)
            CompletedRangesCount = Math.Max(CompletedRangesCount, completedRangesCount.Value);
        if (rangeProgressPercent.HasValue)
            RangeProgressPercent = ClampPercent(rangeProgressPercent.Value);
        LastHeartbeatAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateSession(string? egressIp, string? tokenRef, string? sessionStatus, DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        EgressIp = NormalizeOptional(egressIp) ?? EgressIp;
        TokenRef = NormalizeOptional(tokenRef) ?? TokenRef;
        SessionStatus = NormalizeOptional(sessionStatus) ?? SessionStatus;
        LastHeartbeatAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Complete(int plannedProductsCount, int downloadedProductsCount, DateTime nowUtc)
    {
        Finish(ParserProxyRunStatuses.Completed, plannedProductsCount, downloadedProductsCount, null, nowUtc);
    }

    public void Fail(int plannedProductsCount, int downloadedProductsCount, string? error, DateTime nowUtc)
    {
        Finish(ParserProxyRunStatuses.Failed, plannedProductsCount, downloadedProductsCount, error, nowUtc);
    }

    private void Finish(
        string status,
        int plannedProductsCount,
        int downloadedProductsCount,
        string? error,
        DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        Status = status;
        Phase = status == ParserProxyRunStatuses.Failed
            ? ParserProxyRunPhases.Failed
            : ParserProxyRunPhases.Completed;
        PlannedProductsCount = Math.Max(PlannedProductsCount, plannedProductsCount);
        DownloadedProductsCount = Math.Max(DownloadedProductsCount, downloadedProductsCount);
        if (status == ParserProxyRunStatuses.Completed)
            RangeProgressPercent = 100;
        Error = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
        LastHeartbeatAtUtc = nowUtc;
        FinishedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    private static void EnsureUtc(DateTime value)
    {
        DateTimeUtc.EnsureUtc(value, nameof(value));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeCycleKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ParserProxyRunCycleKinds.Legacy;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is ParserProxyRunCycleKinds.Production or ParserProxyRunCycleKinds.Diagnostic or ParserProxyRunCycleKinds.Legacy
            ? normalized
            : ParserProxyRunCycleKinds.Legacy;
    }

    private static string NormalizePhase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ParserProxyRunPhases.Download;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is ParserProxyRunPhases.Ranges or ParserProxyRunPhases.Download or ParserProxyRunPhases.Completed or ParserProxyRunPhases.Failed
            ? normalized
            : ParserProxyRunPhases.Download;
    }

    private static double ClampPercent(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return 0;
        return Math.Round(Math.Clamp(value, 0, 100), 1);
    }

    private static string PipelineRunKey(string externalProxyRunId)
    {
        var separatorIndex = externalProxyRunId.IndexOf(':', StringComparison.Ordinal);
        return separatorIndex > 0 ? externalProxyRunId[..separatorIndex] : externalProxyRunId;
    }
}
