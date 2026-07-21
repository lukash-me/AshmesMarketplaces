namespace AshmesMarketplaces.Application.Reviews.Dtos;

public sealed record ReviewResponse(
    Guid Id,
    Guid IdProduct,
    string IdOnMp,
    int Rating,
    string? Text,
    bool IsReplied,
    DateTime DateCreate,
    DateTime? DateReply);
