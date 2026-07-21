using System.Text.Json;

namespace AshmesMarketplaces.Application.Campaigns.Dtos;

public sealed record CampaignResponse(
    Guid Id,
    Guid IdProduct,
    Guid? IdSetCampaign,
    string Name,
    decimal? Budget,
    string? Region,
    int Status,
    int Type,
    string? Description,
    JsonElement? TimeToImpression,
    DateTime? DateStart,
    DateTime? DateEnd,
    DateTime DateCreate,
    DateTime DateUpdate);
