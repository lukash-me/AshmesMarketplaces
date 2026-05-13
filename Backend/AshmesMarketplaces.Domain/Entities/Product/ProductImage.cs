using AshmesMarketplaces.Domain.IDs;

namespace AshmesMarketplaces.Domain.Entities.Product;

public class ProductImage
{
    public Guid Id { get; private set; }
    public ProductId IdProduct { get; private set; } = ProductId.EmptyId();
    public string Url { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsMain { get; private set; }
    
    private ProductImage() { }

    public ProductImage(string url, int sortOrder)
        : this(ProductId.EmptyId(), url, sortOrder, sortOrder == 1)
    {
    }

    public ProductImage(ProductId idProduct, string url, int sortOrder, bool isMain)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Url is required");

        if (sortOrder <= 0)
            throw new ArgumentException("SortOrder must be > 0");

        Id = Guid.NewGuid();
        IdProduct = idProduct;
        Url = url;
        SortOrder = sortOrder;
        IsMain = isMain;
    }
}
