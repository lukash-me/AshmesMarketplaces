namespace AshmesMarketplaces.Application.Products.Dtos;

public sealed record ProductHistoryListItemResponse(
    Guid Id,
    Guid IdProduct,
    decimal? Cost,
    decimal Price,
    int Discount,
    bool IsActive,
    bool? IsAutoDiscountActive,
    DateTime Date);
