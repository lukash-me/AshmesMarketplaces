namespace AshmesMarketplaces.Application.Expenses.Dtos;

public sealed class ExpenseListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdWorkspace { get; init; }
    public Guid? IdCategory { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? IdCreator { get; init; }
    public Guid? IdResponsible { get; init; }
    public Guid? ResponsibleUserId { get; init; }
    public int? Status { get; init; }
    public string? StatusKey { get; init; }
    public DateTime? DatePayFrom { get; init; }
    public DateTime? DatePayTo { get; init; }
    public DateTime? DateCreateFrom { get; init; }
    public DateTime? DateCreateTo { get; init; }
    public decimal? AmountFrom { get; init; }
    public decimal? AmountTo { get; init; }
}
