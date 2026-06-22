namespace AshmesMarketplaces.Application.MarketIntelligence.Dtos;

public sealed record SchedulePublicAnalysisRefreshResponse(
    string ScheduleKey,
    string Status,
    DateTime NextRunAtUtc);
