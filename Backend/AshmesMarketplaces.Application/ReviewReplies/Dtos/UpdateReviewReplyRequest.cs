namespace AshmesMarketplaces.Application.ReviewReplies.Dtos;

public sealed class UpdateReviewReplyRequest
{
    public Guid IdReview { get; init; }
    public string IdOnMp { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public int Status { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
