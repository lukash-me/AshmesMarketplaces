using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Logistics;

public class Logistic
{
    private Logistic() { }

    public Logistic(
        ProductId idProduct,
        Guid? idWarehouse,
        int? stockAmount,
        int stockAmountStatistic,
        int? stockInTransit,
        decimal? costStorage,
        decimal? costLogistic,
        int type,
        DateTime date)
    {
        if (idProduct.Value == Guid.Empty)
            throw new ArgumentException("Product id is required", nameof(idProduct));

        if (stockAmount is < 0)
            throw new ArgumentOutOfRangeException(nameof(stockAmount), "StockAmount must be non-negative");

        if (stockAmountStatistic < 0)
            throw new ArgumentOutOfRangeException(nameof(stockAmountStatistic), "StockAmountStatistic must be non-negative");

        if (stockInTransit is < 0)
            throw new ArgumentOutOfRangeException(nameof(stockInTransit), "StockInTransit must be non-negative");

        if (costStorage is < 0)
            throw new ArgumentOutOfRangeException(nameof(costStorage), "CostStorage must be non-negative");

        if (costLogistic is < 0)
            throw new ArgumentOutOfRangeException(nameof(costLogistic), "CostLogistic must be non-negative");

        DateTimeUtc.EnsureUtc(date, nameof(date));

        Id = Guid.NewGuid();
        IdProduct = idProduct;
        IdWarehouse = idWarehouse;
        StockAmount = stockAmount;
        StockAmountStatistic = stockAmountStatistic;
        StockInTransit = stockInTransit;
        CostStorage = costStorage;
        CostLogistic = costLogistic;
        Type = type;
        Date = date;
    }

    public Guid Id { get; private set; }
    public ProductId IdProduct { get; private set; } = ProductId.EmptyId();
    public Guid? IdWarehouse { get; private set; }
    public int? StockAmount { get; private set; }
    public int StockAmountStatistic { get; private set; }
    public int? StockInTransit { get; private set; }
    public decimal? CostStorage { get; private set; }
    public decimal? CostLogistic { get; private set; }
    public int Type { get; private set; } // enum по документации, значения не определены
    public DateTime Date { get; private set; }
}
