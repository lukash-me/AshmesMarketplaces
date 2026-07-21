using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public sealed class PublicAnalysisRefreshScheduler : IPublicAnalysisRefreshScheduler
{
    private readonly ApplicationDbContext _dbContext;

    public PublicAnalysisRefreshScheduler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<SchedulePublicAnalysisRefreshResponse>> RequestRunAsync(
        string scheduleKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scheduleKey))
            return ServiceResult<SchedulePublicAnalysisRefreshResponse>.BadRequest("Schedule key is required.");

        var normalizedScheduleKey = scheduleKey.Trim();
        var schedule = await _dbContext.PublicAnalysisSchedules
            .FirstOrDefaultAsync(x => x.ScheduleKey == normalizedScheduleKey, cancellationToken);
        if (schedule is null)
            return ServiceResult<SchedulePublicAnalysisRefreshResponse>.NotFound("Public analysis schedule was not found.");

        var requestedAtUtc = DateTime.UtcNow;
        schedule.RequestRun(requestedAtUtc);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SchedulePublicAnalysisRefreshResponse>.Success(
            new SchedulePublicAnalysisRefreshResponse(
                schedule.ScheduleKey,
                schedule.LastStatus ?? "pending",
                schedule.NextRunAtUtc));
    }
}
