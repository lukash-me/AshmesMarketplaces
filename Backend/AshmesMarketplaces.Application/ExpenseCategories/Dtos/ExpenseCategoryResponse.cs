namespace AshmesMarketplaces.Application.ExpenseCategories.Dtos;

public sealed record ExpenseCategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime DateCreate,
    DateTime DateUpdate);
