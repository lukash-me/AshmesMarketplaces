namespace AshmesMarketplaces.Application.ReviewReplies.Dtos;

public sealed record ReviewReplyListItemResponse(
    Guid Id,
    Guid IdReview,
    string IdOnMp,
    string Text,
    int Status,
    DateTime DateCreate,
    DateTime DateUpdate);
