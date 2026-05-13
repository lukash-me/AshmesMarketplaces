namespace AshmesMarketplaces.Domain.Entities.Finance;

public class ExpenseCategory
{
    private ExpenseCategory() { }

    public ExpenseCategory(string name, string? description, DateTime dateCreate, DateTime dateUpdate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (dateUpdate < dateCreate)
            throw new ArgumentException("DateUpdate cannot be earlier than DateCreate", nameof(dateUpdate));

        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        DateCreate = dateCreate;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime DateCreate { get; private set; }
    public DateTime DateUpdate { get; private set; }
}
