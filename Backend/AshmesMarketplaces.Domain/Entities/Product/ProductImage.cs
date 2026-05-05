namespace AshmesMarketplaces.Domain.Entities.Product;

public class ProductImage
{
    public Guid Id { get; private set; }
    public string Url { get; private set; }
    public int SortOrder { get; private set; }
    
    private ProductImage() { }
    public ProductImage(string url, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Url is required");

        if (sortOrder <= 0)
            throw new ArgumentException("SortOrder must be > 0");

        Id = Guid.NewGuid();
        Url = url;
        SortOrder = sortOrder;
    }
}