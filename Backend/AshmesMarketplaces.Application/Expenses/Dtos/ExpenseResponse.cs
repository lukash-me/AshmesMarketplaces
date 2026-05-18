namespace AshmesMarketplaces.Application.Expenses.Dtos;

public sealed record ExpenseResponse(
    Guid Id,
    Guid IdWorkspace,
    Guid? IdCategory,
    Guid IdCreator,
    Guid? IdResponsible,
    string Name,
    string? Description,
    decimal? Cost,
    int Status,
    DateTime? DatePay,
    DateTime DateCreate,
    DateTime DateUpdate);
