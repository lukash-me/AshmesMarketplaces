namespace AshmesMarketplaces.Application.ExpenseCategories.Dtos;

public sealed record ExpenseCategoryListItemResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime DateCreate,
    DateTime DateUpdate);
