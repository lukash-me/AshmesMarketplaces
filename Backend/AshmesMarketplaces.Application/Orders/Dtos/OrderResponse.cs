namespace AshmesMarketplaces.Application.Orders.Dtos;

public sealed record OrderResponse(
    Guid Id,
    Guid IdProduct,
    decimal Price,
    decimal Discount,
    int Amount,
    string? LocationSource,
    string? LocationDestination,
    int Status,
    DateTime? DateDelivered,
    DateTime DateOpened,
    DateTime? DateClosed,
    DateTime DateUpdate);
