using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public interface IPublicAnalysisRefreshScheduler
{
    Task<ServiceResult<SchedulePublicAnalysisRefreshResponse>> RequestRunAsync(
        string scheduleKey,
        CancellationToken cancellationToken);
}
