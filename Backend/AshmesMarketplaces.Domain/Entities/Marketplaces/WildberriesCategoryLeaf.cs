using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Marketplaces;

public sealed class WildberriesCategoryLeaf
{
    private WildberriesCategoryLeaf() { }

    public WildberriesCategoryLeaf(
        long wbCategoryId,
        string name,
        string sourceCategory,
        string sourceSubcategory,
        string sourcePath,
        string? searchQuery,
        long? parentId,
        int level,
        DateTime fetchedAtUtc,
        DateTime nowUtc)
    {
        if (wbCategoryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(wbCategoryId), "WB category id is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("WB category name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(sourceCategory))
            throw new ArgumentException("Source category is required.", nameof(sourceCategory));
        if (string.IsNullOrWhiteSpace(sourceSubcategory))
            throw new ArgumentException("Source subcategory is required.", nameof(sourceSubcategory));
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));

        DateTimeUtc.EnsureUtc(fetchedAtUtc, nameof(fetchedAtUtc));
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        Id = Guid.NewGuid();
        WbCategoryId = wbCategoryId;
        Name = name.Trim();
        SourceCategory = sourceCategory.Trim();
        SourceSubcategory = sourceSubcategory.Trim();
        SourcePath = sourcePath.Trim();
        SearchQuery = string.IsNullOrWhiteSpace(searchQuery) ? null : searchQuery.Trim();
        ParentId = parentId;
        IsLeaf = true;
        Level = level;
        FetchedAtUtc = fetchedAtUtc;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public long WbCategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string SourceCategory { get; private set; } = string.Empty;
    public string SourceSubcategory { get; private set; } = string.Empty;
    public string SourcePath { get; private set; } = string.Empty;
    public string? SearchQuery { get; private set; }
    public long? ParentId { get; private set; }
    public bool IsLeaf { get; private set; } = true;
    public int Level { get; private set; }
    public DateTime FetchedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateFromCatalog(
        string name,
        string sourceCategory,
        string sourceSubcategory,
        string sourcePath,
        string? searchQuery,
        long? parentId,
        int level,
        DateTime fetchedAtUtc,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("WB category name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(sourceCategory))
            throw new ArgumentException("Source category is required.", nameof(sourceCategory));
        if (string.IsNullOrWhiteSpace(sourceSubcategory))
            throw new ArgumentException("Source subcategory is required.", nameof(sourceSubcategory));
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));

        DateTimeUtc.EnsureUtc(fetchedAtUtc, nameof(fetchedAtUtc));
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        Name = name.Trim();
        SourceCategory = sourceCategory.Trim();
        SourceSubcategory = sourceSubcategory.Trim();
        SourcePath = sourcePath.Trim();
        SearchQuery = string.IsNullOrWhiteSpace(searchQuery) ? null : searchQuery.Trim();
        ParentId = parentId;
        Level = level;
        FetchedAtUtc = fetchedAtUtc;
        UpdatedAtUtc = nowUtc;
    }
}
