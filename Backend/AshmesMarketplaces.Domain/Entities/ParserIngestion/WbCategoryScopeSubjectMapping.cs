using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public static class WbCategoryScopeSubjectMappingStatuses
{
    public const string Active = "active";
    public const string Rejected = "rejected";
    public const string NeedsReview = "needs_review";
}

public static class WbCategoryScopeSubjectMappingSources
{
    public const string Observed = "observed";
    public const string Manual = "manual";
    public const string WbCatalog = "wb_catalog";
}

public sealed class WbCategoryScopeSubjectMapping
{
    private WbCategoryScopeSubjectMapping() { }

    public WbCategoryScopeSubjectMapping(
        long wbMenuId,
        string menuToken,
        string sourcePath,
        long subjectId,
        string? subjectName,
        string status,
        string mappingSource,
        DateTime observedAtUtc)
    {
        if (wbMenuId <= 0)
            throw new ArgumentOutOfRangeException(nameof(wbMenuId), "WB menu id is required.");
        if (string.IsNullOrWhiteSpace(menuToken))
            throw new ArgumentException("Menu token is required.", nameof(menuToken));
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        if (subjectId <= 0)
            throw new ArgumentOutOfRangeException(nameof(subjectId), "Subject id is required.");
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status is required.", nameof(status));
        if (string.IsNullOrWhiteSpace(mappingSource))
            throw new ArgumentException("Mapping source is required.", nameof(mappingSource));

        DateTimeUtc.EnsureUtc(observedAtUtc, nameof(observedAtUtc));

        Id = Guid.NewGuid();
        WbMenuId = wbMenuId;
        MenuToken = menuToken.Trim();
        SourcePath = sourcePath.Trim();
        SubjectId = subjectId;
        SubjectName = string.IsNullOrWhiteSpace(subjectName) ? null : subjectName.Trim();
        Status = status.Trim();
        MappingSource = mappingSource.Trim();
        ObservedAtUtc = observedAtUtc;
        UpdatedAtUtc = observedAtUtc;
    }

    public Guid Id { get; private set; }
    public long WbMenuId { get; private set; }
    public string MenuToken { get; private set; } = string.Empty;
    public string SourcePath { get; private set; } = string.Empty;
    public long SubjectId { get; private set; }
    public string? SubjectName { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string MappingSource { get; private set; } = string.Empty;
    public DateTime ObservedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateObserved(string? subjectName, DateTime observedAtUtc)
    {
        DateTimeUtc.EnsureUtc(observedAtUtc, nameof(observedAtUtc));

        if (!string.IsNullOrWhiteSpace(subjectName))
            SubjectName = subjectName.Trim();
        ObservedAtUtc = observedAtUtc;
        UpdatedAtUtc = observedAtUtc;
    }

    public void ChangeStatus(string status, string mappingSource, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status is required.", nameof(status));
        if (string.IsNullOrWhiteSpace(mappingSource))
            throw new ArgumentException("Mapping source is required.", nameof(mappingSource));

        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        Status = status.Trim();
        MappingSource = mappingSource.Trim();
        UpdatedAtUtc = nowUtc;
    }
}
