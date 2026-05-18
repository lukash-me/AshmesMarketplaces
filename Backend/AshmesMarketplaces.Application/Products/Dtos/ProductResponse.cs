using System.Text.Json;
using AshmesMarketplaces.Domain.Entities.Product;

namespace AshmesMarketplaces.Application.Products.Dtos;

public sealed record ProductResponse(
    Guid Id,
    Guid IdMp,
    Guid? IdBrand,
    Guid? IdCategory,
    string? IdOnMp,
    string? SkuProduct,
    string SkuSeller,
    string Name,
    string? Description,
    JsonElement? Characteristics,
    string? Barcode,
    int? Commission,
    ProductStatus Status,
    DateTime? DateCreated,
    DateTime DateUpdated,
    IReadOnlyCollection<ProductImageResponse> Images,
    IReadOnlyCollection<ProductVideoResponse> Videos);
