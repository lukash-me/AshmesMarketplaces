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
    string? StatusKey,
    string StatusLabel,
    string? CategoryName,
    string WorkspaceName,
    string CreatorLogin,
    string? CreatorEmail,
    string? ResponsibleLogin,
    string? ResponsibleEmail,
    DateTime? DatePay,
    DateTime DateCreate,
    DateTime DateUpdate);
