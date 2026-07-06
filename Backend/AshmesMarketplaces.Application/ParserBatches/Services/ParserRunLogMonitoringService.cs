using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public interface IParserRunLogMonitoringService
{
    Task ProcessOnceAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

public sealed class ParserRunLogMonitoringService : IParserRunLogMonitoringService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IParserLogReader _logReader;

    public ParserRunLogMonitoringService(
        ApplicationDbContext dbContext,
        IParserLogReader logReader)
    {
        _dbContext = dbContext;
        _logReader = logReader;
    }

    public async Task ProcessOnceAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            nowUtc = DateTime.SpecifyKind(nowUtc.ToUniversalTime(), DateTimeKind.Utc);

        var runs = await _dbContext.ParserProxyRuns
            .Where(x => x.Status == ParserProxyRunStatuses.Running)
            .OrderBy(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

        var staleAfter = TimeSpan.FromSeconds(Math.Max(30, _logReader.Options.StaleAfterSeconds));
        var interruptedInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasChanges = false;
        foreach (var run in runs)
        {
            var snapshot = _logReader.ReadProxyRun(run.ParserCycleId, run.ProxyKey);
            var lastActivityAtUtc = run.LastHeartbeatAtUtc;
            if (snapshot is not null)
            {
                lastActivityAtUtc = snapshot.LastLogAtUtc ?? lastActivityAtUtc;
                run.UpdateProgress(
                    snapshot.PlannedProductsCount,
                    snapshot.DownloadedProductsCount,
                    lastActivityAtUtc,
                    snapshot.Phase,
                    rangeChecksCount: snapshot.RangeChecksCount,
                    finalRangesCount: snapshot.FinalRangesCount,
                    emptyRangesCount: snapshot.EmptyRangesCount,
                    splitRangesCount: snapshot.SplitRangesCount);
            }

            if (nowUtc - lastActivityAtUtc > staleAfter)
            {
                run.Interrupt(
                    snapshot?.PlannedProductsCount ?? run.PlannedProductsCount,
                    snapshot?.DownloadedProductsCount ?? run.DownloadedProductsCount,
                    $"No parser log activity since {lastActivityAtUtc:O}.",
                    nowUtc);
                interruptedInstanceIds.Add(run.ParserInstanceId);
                hasChanges = true;
            }
        }

        foreach (var parserInstanceId in interruptedInstanceIds)
        {
            if (runs.Any(x => x.ParserInstanceId == parserInstanceId && x.Status == ParserProxyRunStatuses.Running))
                continue;

            var activeLaunches = await _dbContext.ParserLaunchRequests
                .Where(x => x.ParserInstanceId == parserInstanceId &&
                            ParserLaunchRequestStatuses.Active.Contains(x.Status))
                .ToListAsync(cancellationToken);

            foreach (var launch in activeLaunches)
            {
                launch.MarkFailed("Parser launch interrupted: no active proxy runs remain.", nowUtc);
                hasChanges = true;
            }
        }

        hasChanges |= await CloseStaleRunningLaunchesWithoutActiveProxyRunsAsync(runs, nowUtc, cancellationToken);

        if (hasChanges)
            await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> CloseStaleRunningLaunchesWithoutActiveProxyRunsAsync(
        IReadOnlyCollection<ParserProxyRun> initiallyRunningRuns,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var runningLaunches = await _dbContext.ParserLaunchRequests
            .Where(x => x.Status == ParserLaunchRequestStatuses.Running)
            .ToListAsync(cancellationToken);
        if (runningLaunches.Count == 0)
            return false;

        var changed = false;
        foreach (var launch in runningLaunches)
        {
            if (initiallyRunningRuns.Any(x =>
                    x.ParserInstanceId == launch.ParserInstanceId &&
                    x.Status == ParserProxyRunStatuses.Running))
            {
                continue;
            }

            var launchStartedAt = launch.StartedAtUtc ?? launch.RequestedAtUtc;
            var hasInterruptedProxyRun = await _dbContext.ParserProxyRuns
                .AnyAsync(
                    x => x.ParserInstanceId == launch.ParserInstanceId &&
                         x.Status == ParserProxyRunStatuses.Interrupted &&
                         x.StartedAtUtc >= launchStartedAt,
                    cancellationToken);
            if (!hasInterruptedProxyRun)
                continue;

            launch.MarkFailed("Parser launch interrupted: no active proxy runs remain.", nowUtc);
            changed = true;
        }

        return changed;
    }
}
