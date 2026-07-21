namespace AshmesMarketplaces.Application.Expenses.Dtos;

public sealed class CreateExpenseRequest
{
    public Guid IdWorkspace { get; init; }
    public Guid? IdCategory { get; init; }
    public Guid IdCreator { get; init; }
    public Guid? IdResponsible { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal? Cost { get; init; }
    public int Status { get; init; }
    public string? StatusKey { get; init; }
    public DateTime? DatePay { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
