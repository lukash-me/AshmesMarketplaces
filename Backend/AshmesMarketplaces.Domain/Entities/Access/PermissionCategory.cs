using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Access;

public class PermissionCategory
{
    private PermissionCategory() { }

    public PermissionCategory(string name, string? description, DateTime dateUpdate, DateTime dateCreate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));
        DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));

        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        DateUpdate = dateUpdate;
        DateCreate = dateCreate;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime DateUpdate { get; private set; }
    public DateTime DateCreate { get; private set; }
}
