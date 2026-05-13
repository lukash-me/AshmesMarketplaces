namespace AshmesMarketplaces.Domain.IDs;

public readonly record struct ProductId(Guid Value)
{
    public static ProductId NewId() => new(Guid.NewGuid());
    public static ProductId EmptyId() => new(Guid.Empty);

    public static ProductId Create(Guid id) => new(id);
    public static implicit operator ProductId(Guid id) => new(id);
    public static implicit operator Guid(ProductId productId) => productId.Value;
}
