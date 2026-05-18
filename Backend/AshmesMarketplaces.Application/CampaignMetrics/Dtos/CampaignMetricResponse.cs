namespace AshmesMarketplaces.Application.CampaignMetrics.Dtos;

public sealed record CampaignMetricResponse(
    Guid Id,
    Guid IdCampaign,
    int? ImpressionAmount,
    int? ClicksAmount,
    decimal? CostDay,
    DateTime Date);
