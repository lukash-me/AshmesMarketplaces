namespace AshmesMarketplaces.Domain.Entities.Marketplaces;

public class BrandMarketplace
{
    private BrandMarketplace() { }

    public BrandMarketplace(Guid idMp, Guid idBrand, string idOnMp)
    {
        if (idMp == Guid.Empty)
            throw new ArgumentException("Marketplace id is required", nameof(idMp));

        if (idBrand == Guid.Empty)
            throw new ArgumentException("Brand id is required", nameof(idBrand));

        if (string.IsNullOrWhiteSpace(idOnMp))
            throw new ArgumentException("Marketplace external id is required", nameof(idOnMp));

        IdMp = idMp;
        IdBrand = idBrand;
        IdOnMp = idOnMp;
    }

    public Guid IdMp { get; private set; }
    public Guid IdBrand { get; private set; }
    public string IdOnMp { get; private set; } = string.Empty;
}
