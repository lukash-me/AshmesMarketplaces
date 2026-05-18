namespace AshmesMarketplaces.Application.Reviews.Dtos;

public sealed class UpdateReviewRequest
{
    public Guid IdProduct { get; init; }
    public string IdOnMp { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Text { get; init; }
    public bool IsReplied { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime? DateReply { get; init; }
}
