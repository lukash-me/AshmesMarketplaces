namespace AshmesMarketplaces.Application.Orders.Dtos;

public sealed class UpdateOrderRequest
{
    public Guid IdProduct { get; init; }
    public decimal Price { get; init; }
    public decimal Discount { get; init; }
    public int Amount { get; init; }
    public string? LocationSource { get; init; }
    public string? LocationDestination { get; init; }
    public int Status { get; init; }
    public DateTime? DateDelivered { get; init; }
    public DateTime DateOpened { get; init; }
    public DateTime? DateClosed { get; init; }
    public DateTime DateUpdate { get; init; }
}
