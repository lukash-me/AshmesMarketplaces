namespace AshmesMarketplaces.Application.Expenses.Dtos;

public sealed record ExpenseListItemResponse(
    Guid Id,
    Guid IdWorkspace,
    Guid? IdCategory,
    Guid IdCreator,
    Guid? IdResponsible,
    string Name,
    decimal? Cost,
    int Status,
    DateTime? DatePay,
    DateTime DateCreate,
    DateTime DateUpdate);
