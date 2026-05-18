namespace AshmesMarketplaces.Application.Products.Dtos;

public sealed class ProductHistoryListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; } = "-date";
}
