namespace AshmesMarketplaces.Domain.Entities.Workspaces;

public class Workspace
{
    private Workspace() { }

    public Workspace(
        Guid? idBrand,
        string name,
        string? description,
        string? urlInvite,
        int status,
        DateTime dateCreate,
        DateTime dateUpdate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (dateUpdate < dateCreate)
            throw new ArgumentException("DateUpdate cannot be earlier than DateCreate", nameof(dateUpdate));

        Id = Guid.NewGuid();
        IdBrand = idBrand;
        Name = name;
        Description = description;
        UrlInvite = urlInvite;
        Status = status;
        DateCreate = dateCreate;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public Guid? IdBrand { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? UrlInvite { get; private set; }
    public int Status { get; private set; } // enum по документации, значения не определены
    public DateTime DateCreate { get; private set; }
    public DateTime DateUpdate { get; private set; }
}
