namespace AshmesMarketplaces.Application.ReviewReplies.Dtos;

public sealed class ReviewReplyListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdReview { get; init; }
    public int? Status { get; init; }
    public DateTime? DateCreateFrom { get; init; }
    public DateTime? DateCreateTo { get; init; }
}
