using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Orders;

public class Order
{
    private Order() { }

    public Order(
        ProductId idProduct,
        decimal price,
        decimal discount,
        int amount,
        string? locationSource,
        string? locationDestination,
        int status,
        DateTime? dateDelivered,
        DateTime dateOpened,
        DateTime? dateClosed,
        DateTime dateUpdate)
    {
        if (idProduct.Value == Guid.Empty)
            throw new ArgumentException("Product id is required", nameof(idProduct));

        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be non-negative");

        if (discount < 0)
            throw new ArgumentOutOfRangeException(nameof(discount), "Discount must be non-negative");

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero");

        DateTimeUtc.EnsureUtc(dateDelivered, nameof(dateDelivered));
        DateTimeUtc.EnsureUtc(dateOpened, nameof(dateOpened));
        DateTimeUtc.EnsureUtc(dateClosed, nameof(dateClosed));
        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        if (dateDelivered.HasValue && dateDelivered < dateOpened)
            throw new ArgumentException("DateDelivered cannot be earlier than DateOpened", nameof(dateDelivered));

        if (dateClosed.HasValue && dateClosed < dateOpened)
            throw new ArgumentException("DateClosed cannot be earlier than DateOpened", nameof(dateClosed));

        Id = Guid.NewGuid();
        IdProduct = idProduct;
        Price = price;
        Discount = discount;
        Amount = amount;
        LocationSource = locationSource;
        LocationDestination = locationDestination;
        Status = status;
        DateDelivered = dateDelivered;
        DateOpened = dateOpened;
        DateClosed = dateClosed;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public ProductId IdProduct { get; private set; } = ProductId.EmptyId();
    public decimal Price { get; private set; }
    public decimal Discount { get; private set; }
    public int Amount { get; private set; }
    public string? LocationSource { get; private set; }
    public string? LocationDestination { get; private set; }
    public int Status { get; private set; } // enum по документации, значения не определены
    public DateTime? DateDelivered { get; private set; }
    public DateTime DateOpened { get; private set; }
    public DateTime? DateClosed { get; private set; }
    public DateTime DateUpdate { get; private set; }
}
