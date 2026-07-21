using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed record ParserProductPresenceReconciliationResult(bool Reconciled, int MarkedMissingCount);

public sealed class ParserProductPresenceReconciliationService
{
    private const string MissingReason = "missing_in_full_cycle";

    private readonly ApplicationDbContext _dbContext;

    public ParserProductPresenceReconciliationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ParserProductPresenceReconciliationResult> TryReconcileCycleAsync(
        string? parserCycleId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parserCycleId))
            return new ParserProductPresenceReconciliationResult(false, 0);

        var cycleId = parserCycleId.Trim();
        var launch = await _dbContext.ParserLaunchRequests
            .AsNoTracking()
            .Where(x => x.ParserCycleId == cycleId)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (launch is null ||
            launch.LaunchMode != ParserLaunchModes.FullAll ||
            launch.Status != ParserLaunchRequestStatuses.Completed)
        {
            return new ParserProductPresenceReconciliationResult(false, 0);
        }

        var hasUnfinishedOrFailedBatch = await _dbContext.ParserBatchSubmissions
            .AsNoTracking()
            .AnyAsync(
                x => x.ParserCycleId == cycleId &&
                     x.Status != ParserBatchStatuses.Completed,
                cancellationToken);
        if (hasUnfinishedOrFailedBatch)
            return new ParserProductPresenceReconciliationResult(false, 0);

        var proxyRuns = await _dbContext.ParserProxyRuns
            .AsNoTracking()
            .Where(x => x.ParserCycleId == cycleId)
            .ToListAsync(cancellationToken);
        if (proxyRuns.Count == 0 ||
            proxyRuns.Any(x => x.Status == ParserProxyRunStatuses.Running))
        {
            return new ParserProductPresenceReconciliationResult(false, 0);
        }

        var successfulScopes = proxyRuns
            .GroupBy(x => new PresenceScope(x.SourceCategory, x.SourceSubcategory))
            .Where(group => group.Any(x => x.Status == ParserProxyRunStatuses.Completed) &&
                            group.All(x => x.Status == ParserProxyRunStatuses.Completed))
            .Select(x => x.Key)
            .Distinct()
            .ToList();
        if (successfulScopes.Count == 0)
            return new ParserProductPresenceReconciliationResult(false, 0);

        var now = DateTime.UtcNow;
        var markedMissing = 0;
        foreach (var scope in successfulScopes)
        {
            var missingProducts = await _dbContext.ParserCurrentProductRows
                .Where(x =>
                    x.SourceCategory == scope.SourceCategory &&
                    x.SourceSubcategory == scope.SourceSubcategory &&
                    x.MarketplacePresenceStatus == ParserMarketplacePresenceStatuses.Active &&
                    x.LastSeenParserCycleId != cycleId)
                .ToListAsync(cancellationToken);

            foreach (var product in missingProducts)
            {
                product.MarkMissingInFullScan(cycleId, now);
                _dbContext.ParserProductPresenceEvents.Add(new ParserProductPresenceEvent(
                    product.WbProductId,
                    cycleId,
                    product.SourceCategory,
                    product.SourceSubcategory,
                    ParserMarketplacePresenceStatuses.Active,
                    ParserMarketplacePresenceStatuses.MissingInLatestFullScan,
                    MissingReason,
                    now));
            }

            markedMissing += missingProducts.Count;
        }

        if (markedMissing > 0)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return new ParserProductPresenceReconciliationResult(true, markedMissing);
    }

    private sealed record PresenceScope(string SourceCategory, string SourceSubcategory);
}
