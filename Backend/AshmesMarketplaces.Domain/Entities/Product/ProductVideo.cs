using AshmesMarketplaces.Domain.IDs;

namespace AshmesMarketplaces.Domain.Entities.Product;

public class ProductVideo
{
    public Guid Id { get; private set; }
    public ProductId IdProduct { get; private set; } = ProductId.EmptyId();
    public string Url { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    
    private ProductVideo() { }

    public ProductVideo(string url)
        : this(ProductId.EmptyId(), url, 1)
    {
    }

    public ProductVideo(ProductId idProduct, string url, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Video url is required");

        if (sortOrder <= 0)
            throw new ArgumentException("SortOrder must be > 0");

        Id = Guid.NewGuid();
        IdProduct = idProduct;
        Url = url;
        SortOrder = sortOrder;
    }
}
