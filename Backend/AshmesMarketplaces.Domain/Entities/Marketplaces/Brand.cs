using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Marketplaces;

public class Brand
{
    private Brand() { }

    public Brand(
        string name,
        bool isVerified,
        string country,
        string manufacturer,
        int salesAmount,
        int rateRedemption,
        int level,
        int type,
        DateTime dateMpRegistration,
        DateTime dateUpdate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country is required", nameof(country));

        if (string.IsNullOrWhiteSpace(manufacturer))
            throw new ArgumentException("Manufacturer is required", nameof(manufacturer));

        if (salesAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(salesAmount), "SalesAmount must be non-negative");

        if (rateRedemption < 0)
            throw new ArgumentOutOfRangeException(nameof(rateRedemption), "RateRedemption must be non-negative");

        if (level < 0)
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be non-negative");

        DateTimeUtc.EnsureUtc(dateMpRegistration, nameof(dateMpRegistration));
        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        Id = Guid.NewGuid();
        Name = name;
        IsVerified = isVerified;
        Country = country;
        Manufacturer = manufacturer;
        SalesAmount = salesAmount;
        RateRedemption = rateRedemption;
        Level = level;
        Type = type;
        DateMpRegistration = dateMpRegistration;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; }
    public string Country { get; private set; } = string.Empty;
    public string Manufacturer { get; private set; } = string.Empty;
    public int SalesAmount { get; private set; }
    public int RateRedemption { get; private set; }
    public int Level { get; private set; }
    public int Type { get; private set; } // enum по документации, значения не определены
    public DateTime DateMpRegistration { get; private set; }
    public DateTime DateUpdate { get; private set; }
}
