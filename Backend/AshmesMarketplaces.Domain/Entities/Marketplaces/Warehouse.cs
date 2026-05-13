using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Marketplaces;

public class Warehouse
{
    private Warehouse() { }

    public Warehouse(
        Guid idMp,
        string name,
        string code,
        string region,
        string? city,
        string? address,
        string? latitude,
        string? longitude,
        bool isActive,
        int type,
        DateTime dateCreate,
        DateTime dateUpdate)
    {
        if (idMp == Guid.Empty)
            throw new ArgumentException("Marketplace id is required", nameof(idMp));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required", nameof(code));

        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required", nameof(region));

        DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));
        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        Id = Guid.NewGuid();
        IdMp = idMp;
        Name = name;
        Code = code;
        Region = region;
        City = city;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        IsActive = isActive;
        Type = type;
        DateCreate = dateCreate;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public Guid IdMp { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    public string? City { get; private set; }
    public string? Address { get; private set; }
    public string? Latitude { get; private set; }
    public string? Longitude { get; private set; }
    public bool IsActive { get; private set; }
    public int Type { get; private set; } // enum по документации, значения не определены
    public DateTime DateCreate { get; private set; }
    public DateTime DateUpdate { get; private set; }
}
