namespace AshmesMarketplaces.Application.Logistics.Dtos;

public sealed class CreateLogisticRequest
{
    public Guid IdProduct { get; init; }
    public Guid? IdWarehouse { get; init; }
    public int? StockAmount { get; init; }
    public int StockAmountStatistic { get; init; }
    public int? StockInTransit { get; init; }
    public decimal? CostStorage { get; init; }
    public decimal? CostLogistic { get; init; }
    public int Type { get; init; }
    public DateTime Date { get; init; }
}
