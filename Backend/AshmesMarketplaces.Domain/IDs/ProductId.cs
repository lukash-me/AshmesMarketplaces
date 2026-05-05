namespace AshmesMarketplaces.Domain.IDs;

public class ProductId
{
    private ProductId(Guid value)
    {
        Value = value;
    }
    public Guid Value { get; }
    
    public static ProductId NewId() => new(Guid.NewGuid());
    public static ProductId EmptyId() => new(Guid.Empty);

    public static ProductId Create(Guid id) => new(id);
    public static implicit operator ProductId(Guid id) => new(id);
    public static implicit operator Guid(ProductId productId)
    {
        ArgumentNullException.ThrowIfNull(productId);
        return productId.Value;
    }
}