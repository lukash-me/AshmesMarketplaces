namespace AshmesMarketplaces.Application.ExpenseCategories.Dtos;

public sealed class ExpenseCategoryListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
}
