using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Finance;

public class Expense
{
    private Expense() { }

    public Expense(
        Guid idWorkspace,
        Guid? idCategory,
        Guid idCreator,
        Guid? idResponsible,
        string name,
        string? description,
        decimal? cost,
        int status,
        DateTime? datePay,
        DateTime dateCreate,
        DateTime dateUpdate)
    {
        if (idWorkspace == Guid.Empty)
            throw new ArgumentException("Workspace id is required", nameof(idWorkspace));

        if (idCreator == Guid.Empty)
            throw new ArgumentException("Creator id is required", nameof(idCreator));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (cost is < 0)
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be non-negative");

        DateTimeUtc.EnsureUtc(datePay, nameof(datePay));
        DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));
        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        if (dateUpdate < dateCreate)
            throw new ArgumentException("DateUpdate cannot be earlier than DateCreate", nameof(dateUpdate));

        Id = Guid.NewGuid();
        IdWorkspace = idWorkspace;
        IdCategory = idCategory;
        IdCreator = idCreator;
        IdResponsible = idResponsible;
        Name = name;
        Description = description;
        Cost = cost;
        Status = status;
        DatePay = datePay;
        DateCreate = dateCreate;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public Guid IdWorkspace { get; private set; }
    public Guid? IdCategory { get; private set; }
    public Guid IdCreator { get; private set; }
    public Guid? IdResponsible { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal? Cost { get; private set; }
    public int Status { get; private set; } // enum по документации, значения не определены
    public DateTime? DatePay { get; private set; }
    public DateTime DateCreate { get; private set; }
    public DateTime DateUpdate { get; private set; }
}
