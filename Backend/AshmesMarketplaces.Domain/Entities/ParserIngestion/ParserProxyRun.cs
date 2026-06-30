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
        DateTime startedAtUtc)
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
        ProxyKey = proxyKey.Trim();
        SourceCategory = sourceCategory.Trim();
        SourceSubcategory = sourceSubcategory.Trim();
        Status = ParserProxyRunStatuses.Running;
        PlannedProductsCount = Math.Max(0, plannedProductsCount);
        DownloadedProductsCount = Math.Max(0, downloadedProductsCount);
        StartedAtUtc = startedAtUtc;
        LastHeartbeatAtUtc = startedAtUtc;
        CreatedAtUtc = startedAtUtc;
        UpdatedAtUtc = startedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ParserInstanceId { get; private set; } = string.Empty;
    public string ExternalProxyRunId { get; private set; } = string.Empty;
    public string ProxyKey { get; private set; } = string.Empty;
    public string SourceCategory { get; private set; } = string.Empty;
    public string SourceSubcategory { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int PlannedProductsCount { get; private set; }
    public int DownloadedProductsCount { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime LastHeartbeatAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateProgress(int plannedProductsCount, int downloadedProductsCount, DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        PlannedProductsCount = Math.Max(PlannedProductsCount, plannedProductsCount);
        DownloadedProductsCount = Math.Max(DownloadedProductsCount, downloadedProductsCount);
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
        PlannedProductsCount = Math.Max(PlannedProductsCount, plannedProductsCount);
        DownloadedProductsCount = Math.Max(DownloadedProductsCount, downloadedProductsCount);
        Error = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
        LastHeartbeatAtUtc = nowUtc;
        FinishedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    private static void EnsureUtc(DateTime value)
    {
        DateTimeUtc.EnsureUtc(value, nameof(value));
    }
}
