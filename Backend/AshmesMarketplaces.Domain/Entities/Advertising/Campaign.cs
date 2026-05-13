using System.Text.Json;
using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Advertising;

public class Campaign : IDisposable
{
    private Campaign() { }

    public Campaign(
        ProductId idProduct,
        Guid? idSetCampaign,
        string name,
        decimal? budget,
        string? region,
        int status,
        int type,
        string? description,
        JsonDocument? timeToImpression,
        DateTime? dateStart,
        DateTime? dateEnd,
        DateTime dateCreate,
        DateTime dateUpdate)
    {
        if (idProduct.Value == Guid.Empty)
            throw new ArgumentException("Product id is required", nameof(idProduct));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (budget is < 0)
            throw new ArgumentOutOfRangeException(nameof(budget), "Budget must be non-negative");

        DateTimeUtc.EnsureUtc(dateStart, nameof(dateStart));
        DateTimeUtc.EnsureUtc(dateEnd, nameof(dateEnd));
        DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));
        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        if (dateStart.HasValue && dateEnd.HasValue && dateEnd < dateStart)
            throw new ArgumentException("DateEnd cannot be earlier than DateStart", nameof(dateEnd));

        if (dateUpdate < dateCreate)
            throw new ArgumentException("DateUpdate cannot be earlier than DateCreate", nameof(dateUpdate));

        Id = Guid.NewGuid();
        IdProduct = idProduct;
        IdSetCampaign = idSetCampaign;
        Name = name;
        Budget = budget;
        Region = region;
        Status = status;
        Type = type;
        Description = description;
        TimeToImpression = timeToImpression;
        DateStart = dateStart;
        DateEnd = dateEnd;
        DateCreate = dateCreate;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public ProductId IdProduct { get; private set; } = ProductId.EmptyId();
    public Guid? IdSetCampaign { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal? Budget { get; private set; }
    public string? Region { get; private set; }
    public int Status { get; private set; } // enum по документации, значения не определены
    public int Type { get; private set; } // enum по документации, значения не определены
    public string? Description { get; private set; }
    public JsonDocument? TimeToImpression { get; private set; }
    public DateTime? DateStart { get; private set; }
    public DateTime? DateEnd { get; private set; }
    public DateTime DateCreate { get; private set; }
    public DateTime DateUpdate { get; private set; }

    public void Dispose()
    {
        TimeToImpression?.Dispose();
    }
}
