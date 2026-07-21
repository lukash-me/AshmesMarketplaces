using System.Security.Cryptography;
using AshmesMarketplaces.Application.Auth.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Auth.Services;

public sealed class UserAnalysisScheduleService : IUserAnalysisScheduleService
{
    public const string DefaultTimezoneId = "Europe/Moscow";
    private const int OverviewDelayMinutes = 10;
    private const int MinutesPerDay = 24 * 60;

    private readonly ApplicationDbContext _dbContext;

    public UserAnalysisScheduleService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthAnalysisScheduleResponse> EnsureScheduleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var schedule = await _dbContext.UserAnalysisSchedules
            .FirstOrDefaultAsync(x => x.IdUser == userId, cancellationToken);
        if (schedule is null)
        {
            var nowUtc = DateTime.UtcNow;
            var hotProductsTime = DeterministicTime(userId);
            var overviewTime = hotProductsTime.AddMinutes(OverviewDelayMinutes);
            schedule = new UserAnalysisSchedule(
                userId,
                DefaultTimezoneId,
                hotProductsTime,
                overviewTime,
                nowUtc,
                nowUtc.AddMinutes(OverviewDelayMinutes),
                nowUtc);
            _dbContext.UserAnalysisSchedules.Add(schedule);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Map(schedule);
    }

    public static DateTime NextDailyUtc(TimeOnly localTime, string timezoneId, DateTime afterUtc)
    {
        var timeZone = ResolveTimeZone(timezoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(afterUtc, timeZone);
        var localCandidate = localNow.Date.Add(localTime.ToTimeSpan());
        if (localCandidate <= localNow)
            localCandidate = localCandidate.AddDays(1);

        return TimeZoneInfo.ConvertTimeToUtc(localCandidate, timeZone);
    }

    private static TimeOnly DeterministicTime(Guid userId)
    {
        var bytes = SHA256.HashData(userId.ToByteArray());
        var value = BitConverter.ToUInt32(bytes, 0);
        return TimeOnly.MinValue.AddMinutes(value % MinutesPerDay);
    }

    private static TimeZoneInfo ResolveTimeZone(string timezoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return ResolveFallbackTimeZone(timezoneId);
        }
        catch (InvalidTimeZoneException)
        {
            return ResolveFallbackTimeZone(timezoneId);
        }
    }

    private static TimeZoneInfo ResolveFallbackTimeZone(string timezoneId)
    {
        if (string.Equals(timezoneId, DefaultTimezoneId, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static AuthAnalysisScheduleResponse Map(UserAnalysisSchedule schedule) =>
        new(
            schedule.TimezoneId,
            schedule.HotProductsLocalTime.ToString("HH:mm"),
            schedule.OverviewLocalTime.ToString("HH:mm"),
            schedule.NextHotProductsRunAtUtc,
            schedule.NextOverviewRunAtUtc,
            schedule.LastHotProductsStartedAtUtc,
            schedule.LastHotProductsCompletedAtUtc,
            schedule.LastHotProductsStatus,
            schedule.LastHotProductsError,
            schedule.LastOverviewStartedAtUtc,
            schedule.LastOverviewCompletedAtUtc,
            schedule.LastOverviewStatus,
            schedule.LastOverviewError);
}
