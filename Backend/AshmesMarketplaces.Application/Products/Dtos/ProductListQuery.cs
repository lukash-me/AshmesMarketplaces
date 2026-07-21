using AshmesMarketplaces.Domain.Entities.Product;

namespace AshmesMarketplaces.Application.Products.Dtos;

public sealed class ProductListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdMp { get; init; }
    public Guid? IdBrand { get; init; }
    public Guid? IdCategory { get; init; }
    public ProductStatus? Status { get; init; }
}
