namespace AshmesMarketplaces.Application.Reviews.Dtos;

public sealed class ReviewListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdProduct { get; init; }
    public bool? IsReplied { get; init; }
    public int? Rating { get; init; }
    public DateTime? DateCreateFrom { get; init; }
    public DateTime? DateCreateTo { get; init; }
}
