namespace AshmesMarketplaces.Application.CampaignMetrics.Dtos;

public sealed record CampaignMetricListItemResponse(
    Guid Id,
    Guid IdCampaign,
    int? ImpressionAmount,
    int? ClicksAmount,
    decimal? CostDay,
    DateTime Date);
