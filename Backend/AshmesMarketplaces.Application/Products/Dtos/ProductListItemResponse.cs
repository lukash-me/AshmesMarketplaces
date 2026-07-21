using AshmesMarketplaces.Domain.Entities.Product;

namespace AshmesMarketplaces.Application.Products.Dtos;

public sealed record ProductListItemResponse(
    Guid Id,
    Guid IdMp,
    Guid? IdBrand,
    Guid? IdCategory,
    string? IdOnMp,
    string? SkuProduct,
    string SkuSeller,
    string Name,
    string? Barcode,
    int? Commission,
    ProductStatus Status,
    DateTime? DateCreated,
    DateTime DateUpdated);
