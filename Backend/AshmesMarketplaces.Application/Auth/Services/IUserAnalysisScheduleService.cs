using AshmesMarketplaces.Application.Auth.Dtos;

namespace AshmesMarketplaces.Application.Auth.Services;

public interface IUserAnalysisScheduleService
{
    Task<AuthAnalysisScheduleResponse> EnsureScheduleAsync(Guid userId, CancellationToken cancellationToken);
}
