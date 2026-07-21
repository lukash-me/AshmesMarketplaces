using AshmesMarketplaces.Application.Auth.Services;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Services;
using AshmesMarketplaces.Application.MarketplaceCategories.Services;
using AshmesMarketplaces.Application.ParserObservability.Services;
using AshmesMarketplaces.Application.WorkspaceOverview.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.AnalyticsWorker.Background;

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
        _logger.LogInformation("Analytics worker started. workerId={WorkerId}", _workerId);

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
        await ProcessManualPublicAnalysisRequestAsync(nowUtc, cancellationToken);
        await ProcessMarketIntelligenceDueAsync(nowUtc, cancellationToken);
        await ProcessMarketLogisticsEventsDueAsync(nowUtc, cancellationToken);
        await ProcessProductAvailabilityDueAsync(nowUtc, cancellationToken);
        await ProcessWildberriesCategoryCatalogDueAsync(nowUtc, cancellationToken);
        await ProcessMarketConcentrationDueAsync(nowUtc, cancellationToken);
        await ProcessTopForecastDueAsync(nowUtc, cancellationToken);
        await ProcessOverviewDueAsync(nowUtc, cancellationToken);
    }

    private async Task ProcessManualPublicAnalysisRequestAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var claim = await ClaimManualPublicAnalysisRequestAsync(nowUtc, cancellationToken);
        if (!claim.HasValue)
            return;

        var startedAtUtc = DateTime.UtcNow;
        var memoryBeforeMb = CurrentMemoryMb();
        _logger.LogInformation(
            "Manual public analysis refresh started. requestId={RequestId} scheduleKey={ScheduleKey} memoryBeforeMb={MemoryBeforeMb}",
            claim.Value.Id,
            claim.Value.ScheduleKey,
            memoryBeforeMb);

        try
        {
            await RunManualPublicAnalysisRequestAsync(claim.Value.ScheduleKey, cancellationToken);
            var completedAtUtc = DateTime.UtcNow;
            await CompleteManualPublicAnalysisRequestAsync(claim.Value.Id, claim.Value.ScheduleKey, completedAtUtc, cancellationToken);
            _logger.LogInformation(
                "Manual public analysis refresh completed. requestId={RequestId} scheduleKey={ScheduleKey} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                claim.Value.Id,
                claim.Value.ScheduleKey,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
        catch (NoDataAnalysisException ex)
        {
            var completedAtUtc = DateTime.UtcNow;
            await NoDataManualPublicAnalysisRequestAsync(claim.Value.Id, claim.Value.ScheduleKey, completedAtUtc, ex.Message, cancellationToken);
            _logger.LogInformation(
                "Manual public analysis refresh completed with no data. requestId={RequestId} scheduleKey={ScheduleKey} durationMs={DurationMs} message={Message}",
                claim.Value.Id,
                claim.Value.ScheduleKey,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailManualPublicAnalysisRequestAsync(claim.Value.Id, claim.Value.ScheduleKey, DateTime.UtcNow, ex.Message, cancellationToken);
            _logger.LogWarning(
                ex,
                "Manual public analysis refresh failed. requestId={RequestId} scheduleKey={ScheduleKey} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                claim.Value.Id,
                claim.Value.ScheduleKey,
                (DateTime.UtcNow - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
    }

    private async Task RunManualPublicAnalysisRequestAsync(string scheduleKey, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        switch (scheduleKey)
        {
            case PublicAnalysisSchedule.MarketLogisticsEventsScheduleKey:
            {
                var service = scope.ServiceProvider.GetRequiredService<IPublicParserObservedLogisticsRefreshService>();
                var result = await service.RefreshAsync(cancellationToken);
                if (!result.IsSuccess)
                    throw CreateRefreshException(result.Error!);

                return;
            }

            case PublicAnalysisSchedule.ProductAvailabilityScheduleKey:
            {
                var service = scope.ServiceProvider.GetRequiredService<IPublicProductAvailabilityRefreshService>();
                var result = await service.RefreshAsync(cancellationToken);
                if (!result.IsSuccess)
                    throw CreateRefreshException(result.Error!);

                return;
            }

            case PublicAnalysisSchedule.ParserCurrentProductsScheduleKey:
            {
                var service = scope.ServiceProvider.GetRequiredService<IPublicParserCurrentProductRefreshService>();
                var result = await service.RefreshAsync(cancellationToken);
                if (!result.IsSuccess)
                    throw CreateRefreshException(result.Error!);

                return;
            }

            case PublicAnalysisSchedule.MarketIntelligenceScheduleKey:
            {
                var service = scope.ServiceProvider.GetRequiredService<IPublicMarketIntelligenceRefreshService>();
                var result = await service.RefreshAllAsync(cancellationToken);
                if (!result.IsSuccess)
                    throw CreateRefreshException(result.Error!);

                return;
            }

            case PublicAnalysisSchedule.MarketConcentrationScheduleKey:
            {
                var service = scope.ServiceProvider.GetRequiredService<IPublicMarketConcentrationRefreshService>();
                var result = await service.RefreshAllAsync(cancellationToken);
                if (!result.IsSuccess)
                    throw CreateRefreshException(result.Error!);

                return;
            }

            default:
                throw new InvalidOperationException($"Manual public analysis schedule '{scheduleKey}' is not supported.");
        }
    }

    private async Task ProcessWildberriesCategoryCatalogDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var scheduleId = await ClaimPublicScheduleAsync(PublicAnalysisSchedule.WildberriesCategoryCatalogScheduleKey, nowUtc, cancellationToken);
        if (!scheduleId.HasValue)
            return;

        var startedAtUtc = DateTime.UtcNow;
        var memoryBeforeMb = CurrentMemoryMb();
        _logger.LogInformation(
            "Scheduled WB category catalog refresh started. scheduleId={ScheduleId} memoryBeforeMb={MemoryBeforeMb}",
            scheduleId.Value,
            memoryBeforeMb);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IWildberriesCategoryCatalogRefreshService>();
            var result = await service.RefreshAsync(cancellationToken);
            var completedAtUtc = DateTime.UtcNow;
            if (!result.IsSuccess)
                throw CreateRefreshException(result.Error!);

            await CompletePublicWeeklyScheduleAsync(scheduleId.Value, completedAtUtc, cancellationToken);
            _logger.LogInformation(
                "Scheduled WB category catalog refresh completed. scheduleId={ScheduleId} leavesCount={LeavesCount} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                result.Value!.LeavesCount,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
        catch (NoDataAnalysisException ex)
        {
            var completedAtUtc = DateTime.UtcNow;
            await NoDataPublicWeeklyScheduleAsync(scheduleId.Value, completedAtUtc, ex.Message, cancellationToken);
            _logger.LogInformation(
                "Scheduled WB category catalog refresh completed with no data. scheduleId={ScheduleId} durationMs={DurationMs} message={Message}",
                scheduleId.Value,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailPublicScheduleAsync(scheduleId.Value, DateTime.UtcNow, ex.Message, cancellationToken);
            _logger.LogWarning(
                ex,
                "Scheduled WB category catalog refresh failed. scheduleId={ScheduleId} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                (DateTime.UtcNow - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
    }

    private async Task ProcessMarketIntelligenceDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var scheduleId = await ClaimPublicScheduleAsync(PublicAnalysisSchedule.MarketIntelligenceScheduleKey, nowUtc, cancellationToken);
        if (!scheduleId.HasValue)
            return;

        var startedAtUtc = DateTime.UtcNow;
        var memoryBeforeMb = CurrentMemoryMb();
        _logger.LogInformation(
            "Scheduled public market intelligence refresh started. scheduleId={ScheduleId} memoryBeforeMb={MemoryBeforeMb}",
            scheduleId.Value,
            memoryBeforeMb);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPublicMarketIntelligenceRefreshService>();
            var result = await service.RefreshAllAsync(cancellationToken);
            var completedAtUtc = DateTime.UtcNow;
            if (!result.IsSuccess)
                throw CreateRefreshException(result.Error!);

            var sampleSize = result.Value!.Sum(x => x.PriceQualityMap.Summary.TotalPoints);
            await CompletePublicScheduleAsync(scheduleId.Value, completedAtUtc, cancellationToken);
            _logger.LogInformation(
                "Scheduled public market intelligence refresh completed. scheduleId={ScheduleId} snapshotCount={SnapshotCount} sampleSize={SampleSize} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                result.Value!.Count,
                sampleSize,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
        catch (NoDataAnalysisException ex)
        {
            var completedAtUtc = DateTime.UtcNow;
            await NoDataPublicScheduleAsync(scheduleId.Value, completedAtUtc, ex.Message, cancellationToken);
            _logger.LogInformation(
                "Scheduled public market intelligence refresh completed with no data. scheduleId={ScheduleId} durationMs={DurationMs} message={Message}",
                scheduleId.Value,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailPublicScheduleAsync(scheduleId.Value, DateTime.UtcNow, ex.Message, cancellationToken);
            _logger.LogWarning(
                ex,
                "Scheduled public market intelligence refresh failed. scheduleId={ScheduleId} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                (DateTime.UtcNow - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
    }

    private async Task ProcessMarketLogisticsEventsDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var scheduleId = await ClaimPublicScheduleAsync(PublicAnalysisSchedule.MarketLogisticsEventsScheduleKey, nowUtc, cancellationToken);
        if (!scheduleId.HasValue)
            return;

        var startedAtUtc = DateTime.UtcNow;
        var memoryBeforeMb = CurrentMemoryMb();
        _logger.LogInformation(
            "Scheduled public logistics event snapshot refresh started. scheduleId={ScheduleId} memoryBeforeMb={MemoryBeforeMb}",
            scheduleId.Value,
            memoryBeforeMb);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPublicParserObservedLogisticsRefreshService>();
            var result = await service.RefreshAsync(cancellationToken);
            var completedAtUtc = DateTime.UtcNow;
            if (!result.IsSuccess)
                throw CreateRefreshException(result.Error!);

            await CompletePublicScheduleAsync(scheduleId.Value, completedAtUtc, cancellationToken);
            _logger.LogInformation(
                "Scheduled public logistics event snapshot refresh completed. scheduleId={ScheduleId} marketEventsCount={MarketEventsCount} stockDecreasesCount={StockDecreasesCount} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                result.Value!.MarketEventsCount,
                result.Value.StockDecreasesCount,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
        catch (NoDataAnalysisException ex)
        {
            var completedAtUtc = DateTime.UtcNow;
            await NoDataPublicScheduleAsync(scheduleId.Value, completedAtUtc, ex.Message, cancellationToken);
            _logger.LogInformation(
                "Scheduled public logistics event snapshot refresh completed with no data. scheduleId={ScheduleId} durationMs={DurationMs} message={Message}",
                scheduleId.Value,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailPublicScheduleAsync(scheduleId.Value, DateTime.UtcNow, ex.Message, cancellationToken);
            _logger.LogWarning(
                ex,
                "Scheduled public logistics event snapshot refresh failed. scheduleId={ScheduleId} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                (DateTime.UtcNow - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
    }

    private async Task ProcessProductAvailabilityDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var scheduleId = await ClaimPublicScheduleAsync(PublicAnalysisSchedule.ProductAvailabilityScheduleKey, nowUtc, cancellationToken);
        if (!scheduleId.HasValue)
            return;

        var startedAtUtc = DateTime.UtcNow;
        var memoryBeforeMb = CurrentMemoryMb();
        _logger.LogInformation(
            "Scheduled public product availability snapshot refresh started. scheduleId={ScheduleId} memoryBeforeMb={MemoryBeforeMb}",
            scheduleId.Value,
            memoryBeforeMb);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPublicProductAvailabilityRefreshService>();
            var result = await service.RefreshAsync(cancellationToken);
            var completedAtUtc = DateTime.UtcNow;
            if (!result.IsSuccess)
                throw CreateRefreshException(result.Error!);

            await CompletePublicScheduleAsync(scheduleId.Value, completedAtUtc, cancellationToken);
            _logger.LogInformation(
                "Scheduled public product availability snapshot refresh completed. scheduleId={ScheduleId} totalCount={TotalCount} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                result.Value!.TotalCount,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
        catch (NoDataAnalysisException ex)
        {
            var completedAtUtc = DateTime.UtcNow;
            await NoDataPublicScheduleAsync(scheduleId.Value, completedAtUtc, ex.Message, cancellationToken);
            _logger.LogInformation(
                "Scheduled public product availability snapshot refresh completed with no data. scheduleId={ScheduleId} durationMs={DurationMs} message={Message}",
                scheduleId.Value,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailPublicScheduleAsync(scheduleId.Value, DateTime.UtcNow, ex.Message, cancellationToken);
            _logger.LogWarning(
                ex,
                "Scheduled public product availability snapshot refresh failed. scheduleId={ScheduleId} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                (DateTime.UtcNow - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
    }

    private async Task ProcessMarketConcentrationDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var scheduleId = await ClaimPublicScheduleAsync(PublicAnalysisSchedule.MarketConcentrationScheduleKey, nowUtc, cancellationToken);
        if (!scheduleId.HasValue)
            return;

        var startedAtUtc = DateTime.UtcNow;
        var memoryBeforeMb = CurrentMemoryMb();
        _logger.LogInformation(
            "Scheduled public market concentration refresh started. scheduleId={ScheduleId} memoryBeforeMb={MemoryBeforeMb}",
            scheduleId.Value,
            memoryBeforeMb);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPublicMarketConcentrationRefreshService>();
            var result = await service.RefreshAllAsync(cancellationToken);
            var completedAtUtc = DateTime.UtcNow;
            if (!result.IsSuccess)
                throw CreateRefreshException(result.Error!);

            await CompletePublicScheduleAsync(scheduleId.Value, completedAtUtc, cancellationToken);
            _logger.LogInformation(
                "Scheduled public market concentration refresh completed. scheduleId={ScheduleId} snapshotCount={SnapshotCount} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                result.Value!.Count,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
        catch (NoDataAnalysisException ex)
        {
            var completedAtUtc = DateTime.UtcNow;
            await NoDataPublicScheduleAsync(scheduleId.Value, completedAtUtc, ex.Message, cancellationToken);
            _logger.LogInformation(
                "Scheduled public market concentration refresh completed with no data. scheduleId={ScheduleId} durationMs={DurationMs} message={Message}",
                scheduleId.Value,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailPublicScheduleAsync(scheduleId.Value, DateTime.UtcNow, ex.Message, cancellationToken);
            _logger.LogWarning(
                ex,
                "Scheduled public market concentration refresh failed. scheduleId={ScheduleId} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                (DateTime.UtcNow - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
    }

    private async Task ProcessTopForecastDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var scheduleId = await ClaimPublicScheduleAsync(PublicAnalysisSchedule.TopForecastScheduleKey, nowUtc, cancellationToken);
        if (!scheduleId.HasValue)
            return;

        var startedAtUtc = DateTime.UtcNow;
        var memoryBeforeMb = CurrentMemoryMb();
        _logger.LogInformation(
            "Scheduled public top-forecast refresh started. scheduleId={ScheduleId} memoryBeforeMb={MemoryBeforeMb}",
            scheduleId.Value,
            memoryBeforeMb);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPublicTopForecastRefreshService>();
            var result = await service.RefreshAsync(cancellationToken);
            var completedAtUtc = DateTime.UtcNow;
            if (!result.IsSuccess)
                throw CreateRefreshException(result.Error!);

            await CompletePublicScheduleAsync(scheduleId.Value, completedAtUtc, cancellationToken);
            _logger.LogInformation(
                "Scheduled public top-forecast refresh completed. scheduleId={ScheduleId} predictionsCount={PredictionsCount} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                result.Value!.PredictionsCount,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
        catch (NoDataAnalysisException ex)
        {
            var completedAtUtc = DateTime.UtcNow;
            await NoDataPublicScheduleAsync(scheduleId.Value, completedAtUtc, ex.Message, cancellationToken);
            _logger.LogInformation(
                "Scheduled public top-forecast refresh completed with no data. scheduleId={ScheduleId} durationMs={DurationMs} message={Message}",
                scheduleId.Value,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailPublicScheduleAsync(scheduleId.Value, DateTime.UtcNow, ex.Message, cancellationToken);
            _logger.LogWarning(
                ex,
                "Scheduled public top-forecast refresh failed. scheduleId={ScheduleId} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                (DateTime.UtcNow - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
    }

    private async Task ProcessOverviewDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var scheduleId = await ClaimOverviewScheduleAsync(nowUtc, cancellationToken);
        if (!scheduleId.HasValue)
            return;

        var startedAtUtc = DateTime.UtcNow;
        var memoryBeforeMb = CurrentMemoryMb();
        _logger.LogInformation(
            "Scheduled overview refresh started. scheduleId={ScheduleId} memoryBeforeMb={MemoryBeforeMb}",
            scheduleId.Value,
            memoryBeforeMb);

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
                "Scheduled overview refresh completed. scheduleId={ScheduleId} workspaceCount={WorkspaceCount} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                workspaceIds.Count,
                (completedAtUtc - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailOverviewScheduleAsync(scheduleId.Value, DateTime.UtcNow, ex.Message, cancellationToken);
            _logger.LogWarning(
                ex,
                "Scheduled overview refresh failed. scheduleId={ScheduleId} durationMs={DurationMs} memoryBeforeMb={MemoryBeforeMb} memoryAfterMb={MemoryAfterMb}",
                scheduleId.Value,
                (DateTime.UtcNow - startedAtUtc).TotalMilliseconds,
                memoryBeforeMb,
                CurrentMemoryMb());
        }
    }

    private async Task<Guid?> ClaimPublicScheduleAsync(string scheduleKey, DateTime nowUtc, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.PublicAnalysisSchedules
            .Where(x =>
                x.ScheduleKey == scheduleKey
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

    private async Task<(Guid Id, string ScheduleKey)?> ClaimManualPublicAnalysisRequestAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = await dbContext.PublicAnalysisManualRunRequests
            .Where(x => x.Status == PublicAnalysisManualRunRequest.QueuedStatus)
            .OrderBy(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (request is null)
            return null;

        var schedule = await dbContext.PublicAnalysisSchedules
            .Where(x =>
                x.ScheduleKey == request.ScheduleKey &&
                (x.LockedUntilUtc == null || x.LockedUntilUtc <= nowUtc))
            .FirstOrDefaultAsync(cancellationToken);
        if (schedule is null)
            return null;

        schedule.Lock(_workerId, nowUtc.Add(LockDuration), nowUtc);
        request.MarkRunning(nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (request.Id, request.ScheduleKey);
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

    private async Task CompletePublicScheduleAsync(Guid scheduleId, DateTime completedAtUtc, CancellationToken cancellationToken)
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

    private async Task CompletePublicWeeklyScheduleAsync(Guid scheduleId, DateTime completedAtUtc, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.PublicAnalysisSchedules.FirstAsync(x => x.Id == scheduleId, cancellationToken);
        schedule.MarkCompleted(completedAtUtc, completedAtUtc.AddDays(7));
        schedule.ReleaseLock(completedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CompleteManualPublicAnalysisRequestAsync(
        Guid requestId,
        string scheduleKey,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = await dbContext.PublicAnalysisManualRunRequests.FirstAsync(x => x.Id == requestId, cancellationToken);
        request.MarkCompleted(completedAtUtc);

        var schedule = await dbContext.PublicAnalysisSchedules
            .FirstOrDefaultAsync(x => x.ScheduleKey == scheduleKey, cancellationToken);
        schedule?.ReleaseLock(completedAtUtc);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task NoDataPublicScheduleAsync(
        Guid scheduleId,
        DateTime completedAtUtc,
        string message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.PublicAnalysisSchedules.FirstAsync(x => x.Id == scheduleId, cancellationToken);
        var nextRunAtUtc = UserAnalysisScheduleService.NextDailyUtc(
            schedule.LocalTime,
            schedule.TimezoneId,
            completedAtUtc);
        schedule.MarkNoData(completedAtUtc, nextRunAtUtc, Truncate(message));
        schedule.ReleaseLock(completedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task NoDataPublicWeeklyScheduleAsync(
        Guid scheduleId,
        DateTime completedAtUtc,
        string message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.PublicAnalysisSchedules.FirstAsync(x => x.Id == scheduleId, cancellationToken);
        schedule.MarkNoData(completedAtUtc, completedAtUtc.AddDays(7), Truncate(message));
        schedule.ReleaseLock(completedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task NoDataManualPublicAnalysisRequestAsync(
        Guid requestId,
        string scheduleKey,
        DateTime completedAtUtc,
        string message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = await dbContext.PublicAnalysisManualRunRequests.FirstAsync(x => x.Id == requestId, cancellationToken);
        request.MarkNoData(completedAtUtc, Truncate(message));

        var schedule = await dbContext.PublicAnalysisSchedules
            .FirstOrDefaultAsync(x => x.ScheduleKey == scheduleKey, cancellationToken);
        schedule?.ReleaseLock(completedAtUtc);

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

    private async Task FailPublicScheduleAsync(Guid scheduleId, DateTime failedAtUtc, string error, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await dbContext.PublicAnalysisSchedules.FirstAsync(x => x.Id == scheduleId, cancellationToken);
        schedule.MarkFailed(failedAtUtc, failedAtUtc.Add(RetryDelay), Truncate(error));
        schedule.ReleaseLock(failedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task FailManualPublicAnalysisRequestAsync(
        Guid requestId,
        string scheduleKey,
        DateTime failedAtUtc,
        string error,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = await dbContext.PublicAnalysisManualRunRequests.FirstAsync(x => x.Id == requestId, cancellationToken);
        request.MarkFailed(failedAtUtc, Truncate(error));

        var schedule = await dbContext.PublicAnalysisSchedules
            .FirstOrDefaultAsync(x => x.ScheduleKey == scheduleKey, cancellationToken);
        schedule?.ReleaseLock(failedAtUtc);

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

    private static long CurrentMemoryMb() =>
        GC.GetTotalMemory(false) / 1024 / 1024;

    private static Exception CreateRefreshException(ServiceError error) =>
        error.Type == ServiceErrorType.NotFound
            ? new NoDataAnalysisException(error.Message)
            : new InvalidOperationException(error.Message);

    private static string Truncate(string value) =>
        value.Length <= 2000 ? value : value[..2000];

    private sealed class NoDataAnalysisException : Exception
    {
        public NoDataAnalysisException(string message) : base(message)
        {
        }
    }
}
