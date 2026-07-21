namespace AshmesMarketplaces.Application.ReviewReplies.Dtos;

public sealed record ReviewReplyResponse(
    Guid Id,
    Guid IdReview,
    string IdOnMp,
    string Text,
    int Status,
    DateTime DateCreate,
    DateTime DateUpdate);
