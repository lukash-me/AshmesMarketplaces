namespace AshmesMarketplaces.Application.Auth.Dtos;

public sealed record AuthAnalysisScheduleResponse(
    string TimezoneId,
    string HotProductsLocalTime,
    string OverviewLocalTime,
    DateTime NextHotProductsRunAtUtc,
    DateTime NextOverviewRunAtUtc,
    DateTime? LastHotProductsStartedAtUtc,
    DateTime? LastHotProductsCompletedAtUtc,
    string? LastHotProductsStatus,
    string? LastHotProductsError,
    DateTime? LastOverviewStartedAtUtc,
    DateTime? LastOverviewCompletedAtUtc,
    string? LastOverviewStatus,
    string? LastOverviewError);
