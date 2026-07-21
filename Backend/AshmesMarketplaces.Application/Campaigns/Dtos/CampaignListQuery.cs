namespace AshmesMarketplaces.Application.Campaigns.Dtos;

public sealed class CampaignListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdProduct { get; init; }
    public Guid? IdSetCampaign { get; init; }
    public int? Status { get; init; }
    public int? Type { get; init; }
    public DateTime? DateStartFrom { get; init; }
    public DateTime? DateStartTo { get; init; }
    public DateTime? DateEndFrom { get; init; }
    public DateTime? DateEndTo { get; init; }
}
