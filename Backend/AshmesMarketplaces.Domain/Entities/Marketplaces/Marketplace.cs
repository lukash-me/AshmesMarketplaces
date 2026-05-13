using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Marketplaces;

public class Marketplace
{
    private Marketplace() { }

    public Marketplace(
        string name,
        string apiUrl,
        string apiVersion,
        string currency,
        string region,
        int typeCommission,
        string schemeDelivery,
        DateTime dateUpdate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(apiUrl))
            throw new ArgumentException("ApiUrl is required", nameof(apiUrl));

        if (string.IsNullOrWhiteSpace(apiVersion))
            throw new ArgumentException("ApiVersion is required", nameof(apiVersion));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required", nameof(currency));

        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required", nameof(region));

        if (string.IsNullOrWhiteSpace(schemeDelivery))
            throw new ArgumentException("SchemeDelivery is required", nameof(schemeDelivery));

        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        Id = Guid.NewGuid();
        Name = name;
        ApiUrl = apiUrl;
        ApiVersion = apiVersion;
        Currency = currency;
        Region = region;
        TypeCommission = typeCommission;
        SchemeDelivery = schemeDelivery;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ApiUrl { get; private set; } = string.Empty;
    public string ApiVersion { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    public int TypeCommission { get; private set; } // enum по документации, значения не определены
    public string SchemeDelivery { get; private set; } = string.Empty;
    public DateTime DateUpdate { get; private set; }
}
