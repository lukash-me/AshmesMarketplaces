namespace AshmesMarketplaces.Application.Campaigns.Dtos;

public sealed record CampaignListItemResponse(
    Guid Id,
    Guid IdProduct,
    Guid? IdSetCampaign,
    string Name,
    decimal? Budget,
    string? Region,
    int Status,
    int Type,
    DateTime? DateStart,
    DateTime? DateEnd,
    DateTime DateCreate,
    DateTime DateUpdate);
