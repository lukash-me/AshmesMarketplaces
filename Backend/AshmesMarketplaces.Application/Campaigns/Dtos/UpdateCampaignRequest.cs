using System.Text.Json;

namespace AshmesMarketplaces.Application.Campaigns.Dtos;

public sealed class UpdateCampaignRequest
{
    public Guid IdProduct { get; init; }
    public Guid? IdSetCampaign { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal? Budget { get; init; }
    public string? Region { get; init; }
    public int Status { get; init; }
    public int Type { get; init; }
    public string? Description { get; init; }
    public JsonElement? TimeToImpression { get; init; }
    public DateTime? DateStart { get; init; }
    public DateTime? DateEnd { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
