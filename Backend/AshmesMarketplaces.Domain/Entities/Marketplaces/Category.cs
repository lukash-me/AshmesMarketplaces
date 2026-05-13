using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Marketplaces;

public class Category
{
    private Category() { }

    public Category(
        Guid? idParentCategory,
        string? idOnMp,
        string name,
        int level,
        bool isActive,
        DateTime dateUpdate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (level < 0)
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be non-negative");

        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        Id = Guid.NewGuid();
        IdParentCategory = idParentCategory;
        IdOnMp = idOnMp;
        Name = name;
        Level = level;
        IsActive = isActive;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public Guid? IdParentCategory { get; private set; }
    public string? IdOnMp { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Level { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime DateUpdate { get; private set; }
}
