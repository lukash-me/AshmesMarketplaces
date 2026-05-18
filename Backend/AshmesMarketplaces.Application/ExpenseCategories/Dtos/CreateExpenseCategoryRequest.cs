namespace AshmesMarketplaces.Application.ExpenseCategories.Dtos;

public sealed class CreateExpenseCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
