using AshmesMarketplaces.Application.Auth.Services;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using AshmesMarketplaces.Application.MarketRecommendations.Services;
using AshmesMarketplaces.Application.WorkspaceOverview.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.API.Background;

public sealed class AnalysisRefreshHostedService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalysisRefreshHostedService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public AnalysisRefreshHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AnalysisRefreshHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled analysis refresh tick failed.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    private async Task ProcessDueSchedulesAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        await ProcessHotProductsDueAsync(nowUtc, cancellationToken);
        await ProcessOverviewDueAsync(nowUtc, cancellationToken);
    }

    private async Task ProcessHotProductsDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var scheduleId = await ClaimPublicHotProductsScheduleAsync(nowUtc, cancellationToken);
        if (!scheduleId.HasValue)
            return;

        var startedAtUtc = DateTime.UtcNow;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IMarketHotProductsRecalculationService>();
            var result = await service.RecalculatePublicAsync(DefaultHotProductsRequest(), cancellationToken);
            var completedAtUtc = DateTime.UtcNow;
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Error!.Message);

            await CompletePublicHotProductsScheduleAsync(scheduleId.Value, completedAtUtc, cancellationToken);
            _logger.LogInformation(
                "Scheduled public hot-products refresh completed. scheduleId={ScheduleId} durationMs={DurationMs}",
                scheduleId.Value,
                (completedAtUtc - startedAtUtc).TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailPublicHotProductsScheduleAsync(scheduleId.Value, DateTime.UtcNow, ex.Message, cancellationToken);
            _logger.LogWarning(
                ex,
                "Scheduled public hot-products refresh failed. scheduleId={ScheduleId}",
                scheduleId.Value);
        }
    }

    private async Task ProcessOverviewDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var scheduleId = await ClaimOverviewScheduleAsync(nowUtc, cancellationToken);
        if (!scheduleId.HasValue)
            return;

        var startedAtUtc = DateTime.UtcNow;
        try
        {
            var userId = await LoadUserIdAsync(scheduleId.Value, cancellationToken);
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var overviewService = scope.ServiceProvider.GetRequiredService<IWorkspaceOverviewService>();
            var workspaceIds = await dbContext.UserWorkspaces
                .AsNoTracking()
                .Where(x => x.IdUser == userId)
                .Select(x => x.IdWorkspace)
                .Distinct()
                .ToListAsync(cancellationToken);

            var errors = new List<string>();
            foreach (var workspaceId in workspaceIds)
            {
                var result = await overviewService.RecalculateForUserAsync(workspaceId, userId, cancellationToken);
                if (!result.IsSuccess)
                    errors.Add($"{workspaceId}: {result.Error!.Message}");
            }

            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("; ", errors));

            var completedAtUtc = DateTime.UtcNow;
            await CompleteOverviewScheduleAsync(scheduleId.Value, completedAtUtc, cancellationToken);
            _logger.LogInformation(
                "Scheduled overview refresh completed. scheduleId={ScheduleId} workspaceCount={WorkspaceCount} durationMs={DurationMs}",
                scheduleId.Value,
                workspaceIds.Count,
                (completedAtUtc - startedAtUtc).TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailOverviewScheduleAsync(scheduleId.Value, DateTime.UtcNow, ex.Message, cancellationToken);
            _logger.LogWarning(
                ex,
                "Scheduled overview refresh failed. scheduleId={ScheduleId}",
                scheduleId.Value);
        }
    }

    private async Task<Guid?> ClaimPublicHotProductsScheduleAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.PublicAnalysisSchedules
            .Where(x =>
                x.ScheduleKey == PublicAnalysisSchedule.HotProductsScheduleKey
                && x.NextRunAtUtc <= nowUtc
                && (x.LockedUntilUtc == null || x.LockedUntilUtc <= nowUtc))
            .OrderBy(x => x.NextRunAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (schedule is null)
            return null;

        schedule.Lock(_workerId, nowUtc.Add(LockDuration), nowUtc);
        schedule.MarkRunning(nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return schedule.Id;
    }

    private async Task<Guid?> ClaimOverviewScheduleAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.UserAnalysisSchedules
            .Where(x =>
                x.NextOverviewRunAtUtc <= nowUtc
                && (x.LockedUntilUtc == null || x.LockedUntilUtc <= nowUtc))
            .OrderBy(x => x.NextOverviewRunAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (schedule is null)
            return null;

        schedule.Lock(_workerId, nowUtc.Add(LockDuration), nowUtc);
        schedule.MarkOverviewRunning(nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return schedule.Id;
    }

    private async Task<Guid> LoadUserIdAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.UserAnalysisSchedules
            .Where(x => x.Id == scheduleId)
            .Select(x => x.IdUser)
            .FirstAsync(cancellationToken);
    }

    private async Task CompletePublicHotProductsScheduleAsync(Guid scheduleId, DateTime completedAtUtc, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.PublicAnalysisSchedules.FirstAsync(x => x.Id == scheduleId, cancellationToken);
        var nextRunAtUtc = UserAnalysisScheduleService.NextDailyUtc(
            schedule.LocalTime,
            schedule.TimezoneId,
            completedAtUtc);
        schedule.MarkCompleted(completedAtUtc, nextRunAtUtc);
        schedule.ReleaseLock(completedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CompleteOverviewScheduleAsync(Guid scheduleId, DateTime completedAtUtc, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.UserAnalysisSchedules.FirstAsync(x => x.Id == scheduleId, cancellationToken);
        var nextRunAtUtc = UserAnalysisScheduleService.NextDailyUtc(
            schedule.OverviewLocalTime,
            schedule.TimezoneId,
            completedAtUtc);
        schedule.MarkOverviewCompleted(completedAtUtc, nextRunAtUtc);
        schedule.ReleaseLock(completedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task FailPublicHotProductsScheduleAsync(Guid scheduleId, DateTime failedAtUtc, string error, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.PublicAnalysisSchedules.FirstAsync(x => x.Id == scheduleId, cancellationToken);
        schedule.MarkFailed(failedAtUtc, failedAtUtc.Add(RetryDelay), Truncate(error));
        schedule.ReleaseLock(failedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task FailOverviewScheduleAsync(Guid scheduleId, DateTime failedAtUtc, string error, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.UserAnalysisSchedules.FirstAsync(x => x.Id == scheduleId, cancellationToken);
        schedule.MarkOverviewFailed(failedAtUtc, failedAtUtc.Add(RetryDelay), Truncate(error));
        schedule.ReleaseLock(failedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static RecalculateHotProductsRequest DefaultHotProductsRequest() =>
        new(
            SourceCategory: null,
            SourceSubcategory: null,
            ProductParserRunId: null,
            RankParserRunId: null,
            MaxProducts: 100000,
            ForceRecalculate: true,
            MaxRecommendations: 1000,
            MinConfidence: null,
            MinProductsForScoring: 5);

    private static string Truncate(string value) =>
        value.Length <= 2000 ? value : value[..2000];
}
