using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserProxyNicheAssignment
{
    private ParserProxyNicheAssignment() { }

    public ParserProxyNicheAssignment(
        Guid proxyId,
        long wbCategoryId,
        string sourceCategory,
        string sourceSubcategory,
        string sourcePath,
        string searchQuery,
        string parserSearchText,
        bool enabled,
        DateTime nowUtc)
    {
        if (proxyId == Guid.Empty)
            throw new ArgumentException("Proxy id is required.", nameof(proxyId));
        if (wbCategoryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(wbCategoryId), "WB category id is required.");
        if (string.IsNullOrWhiteSpace(sourceCategory))
            throw new ArgumentException("Source category is required.", nameof(sourceCategory));
        if (string.IsNullOrWhiteSpace(sourceSubcategory))
            throw new ArgumentException("Source subcategory is required.", nameof(sourceSubcategory));
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        if (string.IsNullOrWhiteSpace(searchQuery))
            throw new ArgumentException("Search query is required.", nameof(searchQuery));
        if (string.IsNullOrWhiteSpace(parserSearchText))
            throw new ArgumentException("Parser search text is required.", nameof(parserSearchText));

        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        Id = Guid.NewGuid();
        ProxyId = proxyId;
        WbCategoryId = wbCategoryId;
        SourceCategory = sourceCategory.Trim();
        SourceSubcategory = sourceSubcategory.Trim();
        SourcePath = sourcePath.Trim();
        SearchQuery = searchQuery.Trim();
        ParserSearchText = parserSearchText.Trim();
        Enabled = enabled;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProxyId { get; private set; }
    public long WbCategoryId { get; private set; }
    public string SourceCategory { get; private set; } = string.Empty;
    public string SourceSubcategory { get; private set; } = string.Empty;
    public string SourcePath { get; private set; } = string.Empty;
    public string SearchQuery { get; private set; } = string.Empty;
    public string ParserSearchText { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public ParserProxy? Proxy { get; private set; }

    public void Update(
        long wbCategoryId,
        string sourceCategory,
        string sourceSubcategory,
        string sourcePath,
        string searchQuery,
        string parserSearchText,
        bool enabled,
        DateTime nowUtc)
    {
        if (wbCategoryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(wbCategoryId), "WB category id is required.");
        if (string.IsNullOrWhiteSpace(sourceCategory))
            throw new ArgumentException("Source category is required.", nameof(sourceCategory));
        if (string.IsNullOrWhiteSpace(sourceSubcategory))
            throw new ArgumentException("Source subcategory is required.", nameof(sourceSubcategory));
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        if (string.IsNullOrWhiteSpace(searchQuery))
            throw new ArgumentException("Search query is required.", nameof(searchQuery));
        if (string.IsNullOrWhiteSpace(parserSearchText))
            throw new ArgumentException("Parser search text is required.", nameof(parserSearchText));

        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        WbCategoryId = wbCategoryId;
        SourceCategory = sourceCategory.Trim();
        SourceSubcategory = sourceSubcategory.Trim();
        SourcePath = sourcePath.Trim();
        SearchQuery = searchQuery.Trim();
        ParserSearchText = parserSearchText.Trim();
        Enabled = enabled;
        UpdatedAtUtc = nowUtc;
    }

    public void Disable(DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        Enabled = false;
        UpdatedAtUtc = nowUtc;
    }
}
