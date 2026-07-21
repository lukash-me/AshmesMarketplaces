namespace AshmesMarketplaces.Application.CampaignMetrics.Dtos;

public sealed class CreateCampaignMetricRequest
{
    public Guid IdCampaign { get; init; }
    public int? ImpressionAmount { get; init; }
    public int? ClicksAmount { get; init; }
    public decimal? CostDay { get; init; }
    public DateTime Date { get; init; }
}
