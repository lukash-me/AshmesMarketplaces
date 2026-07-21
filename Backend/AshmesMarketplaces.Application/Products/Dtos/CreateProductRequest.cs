using System.Text.Json;
using AshmesMarketplaces.Domain.Entities.Product;

namespace AshmesMarketplaces.Application.Products.Dtos;

public sealed class CreateProductRequest
{
    public Guid IdMp { get; init; }
    public Guid? IdBrand { get; init; }
    public Guid? IdCategory { get; init; }
    public string? IdOnMp { get; init; }
    public string? SkuProduct { get; init; }
    public string SkuSeller { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public JsonElement? Characteristics { get; init; }
    public string? Barcode { get; init; }
    public int? Commission { get; init; }
    public ProductStatus Status { get; init; }
    public DateTime DateUpdated { get; init; }
    public DateTime? DateCreated { get; init; }
    public IReadOnlyCollection<string> ImageUrls { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> VideoUrls { get; init; } = Array.Empty<string>();
}
