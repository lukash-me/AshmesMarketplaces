using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Rules;

public class Rule
{
    private Rule() { }

    public Rule(
        string name,
        string? description,
        string code,
        int? number,
        int domain,
        DateTime dateCreate,
        DateTime dateUpdate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required", nameof(code));

        DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));
        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Code = code;
        Number = number;
        Domain = domain;
        DateCreate = dateCreate;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public int? Number { get; private set; }
    public int Domain { get; private set; } // enum по документации, значения не определены
    public DateTime DateCreate { get; private set; }
    public DateTime DateUpdate { get; private set; }
}
