using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Product;

public class ProductHistory
{
    private ProductHistory() { }

    public ProductHistory(
        ProductId idProduct,
        decimal? cost,
        decimal price,
        int discount,
        bool isActive,
        bool? isAutoDiscountActive,
        DateTime date)
    {
        if (idProduct.Value == Guid.Empty)
            throw new ArgumentException("Product id is required", nameof(idProduct));

        if (cost is < 0)
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be non-negative");

        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be non-negative");

        if (discount < 0)
            throw new ArgumentOutOfRangeException(nameof(discount), "Discount must be non-negative");

        DateTimeUtc.EnsureUtc(date, nameof(date));

        Id = Guid.NewGuid();
        IdProduct = idProduct;
        Cost = cost;
        Price = price;
        Discount = discount;
        IsActive = isActive;
        IsAutoDiscountActive = isAutoDiscountActive;
        Date = date;
    }

    public Guid Id { get; private set; }
    public ProductId IdProduct { get; private set; } = ProductId.EmptyId();
    public decimal? Cost { get; private set; }
    public decimal Price { get; private set; }
    public int Discount { get; private set; }
    public bool IsActive { get; private set; }
    public bool? IsAutoDiscountActive { get; private set; }
    public DateTime Date { get; private set; }
}
