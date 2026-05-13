namespace AshmesMarketplaces.Domain.Entities.Access;

public class Permission
{
    private Permission() { }

    public Permission(
        Guid idCategory,
        string name,
        string description,
        int domain,
        DateTime dateCreate,
        DateTime dateUpdate)
    {
        if (idCategory == Guid.Empty)
            throw new ArgumentException("Category id is required", nameof(idCategory));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));

        Id = Guid.NewGuid();
        IdCategory = idCategory;
        Name = name;
        Description = description;
        Domain = domain;
        DateCreate = dateCreate;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public Guid IdCategory { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int Domain { get; private set; } // enum по документации, значения не определены
    public DateTime DateCreate { get; private set; }
    public DateTime DateUpdate { get; private set; }
}
