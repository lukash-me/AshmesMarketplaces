namespace AshmesMarketplaces.Application.Logistics.Dtos;

public sealed class LogisticListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdProduct { get; init; }
    public Guid? IdWarehouse { get; init; }
    public int? Type { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
}
