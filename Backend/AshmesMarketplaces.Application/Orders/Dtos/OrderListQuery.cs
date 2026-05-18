namespace AshmesMarketplaces.Application.Orders.Dtos;

public sealed class OrderListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdProduct { get; init; }
    public int? Status { get; init; }
    public DateTime? DateOpenedFrom { get; init; }
    public DateTime? DateOpenedTo { get; init; }
}
