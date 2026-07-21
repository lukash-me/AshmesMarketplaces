namespace AshmesMarketplaces.Application.Logistics.Dtos;

public sealed record LogisticResponse(
    Guid Id,
    Guid IdProduct,
    Guid? IdWarehouse,
    int? StockAmount,
    int StockAmountStatistic,
    int? StockInTransit,
    decimal? CostStorage,
    decimal? CostLogistic,
    int Type,
    DateTime Date);
