using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserBatches;

public sealed class ParserProductPresenceReconciliationServiceTests
{
    [Fact]
    public async Task FullAllCycleMarksOnlyUnseenProductsInCompletedScopeAsMissing()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 7, 5, 10);
        var run = CompletedProxyRun("cycle-full", "category", "niche", now);
        var launch = CompletedLaunch(ParserLaunchModes.FullAll, "cycle-full", now);
        var completedBatch = CompletedBatch(run, "cycle-full:batch:0001");
        var seen1 = CurrentProduct("1001", "category", "niche");
        var seen2 = CurrentProduct("1002", "category", "niche");
        var unseen = CurrentProduct("1003", "category", "niche");
        var otherScope = CurrentProduct("2001", "category", "other-niche");
        seen1.MarkSeen("cycle-full", run.Id, now);
        seen2.MarkSeen("cycle-full", run.Id, now);

        context.ParserLaunchRequests.Add(launch);
        context.ParserProxyRuns.Add(run);
        context.ParserBatchSubmissions.Add(completedBatch);
        context.ParserCurrentProductRows.AddRange(seen1, seen2, unseen, otherScope);
        await context.SaveChangesAsync();

        var service = new ParserProductPresenceReconciliationService(context);
        var result = await service.TryReconcileCycleAsync("cycle-full", CancellationToken.None);

        Assert.True(result.Reconciled);
        Assert.Equal(1, result.MarkedMissingCount);
        var products = await context.ParserCurrentProductRows
            .OrderBy(x => x.WbProductId)
            .Select(x => new { x.WbProductId, x.MarketplacePresenceStatus, x.LastPresenceCheckedParserCycleId })
            .ToListAsync();
        Assert.Equal(ParserMarketplacePresenceStatuses.Active, products.Single(x => x.WbProductId == "1001").MarketplacePresenceStatus);
        Assert.Equal(ParserMarketplacePresenceStatuses.Active, products.Single(x => x.WbProductId == "1002").MarketplacePresenceStatus);
        Assert.Equal(ParserMarketplacePresenceStatuses.MissingInLatestFullScan, products.Single(x => x.WbProductId == "1003").MarketplacePresenceStatus);
        Assert.Equal(ParserMarketplacePresenceStatuses.Active, products.Single(x => x.WbProductId == "2001").MarketplacePresenceStatus);
        Assert.Equal("cycle-full", products.Single(x => x.WbProductId == "1003").LastPresenceCheckedParserCycleId);
        Assert.Equal(1, await context.ParserProductPresenceEvents.CountAsync());
    }

    [Theory]
    [InlineData(ParserLaunchModes.CheckProxy)]
    [InlineData(ParserLaunchModes.LimitedAll)]
    public async Task NonFullCycleDoesNotMarkMissing(string launchMode)
    {
        await using var context = CreateContext();
        var now = Utc(2026, 7, 5, 10);
        var run = CompletedProxyRun("cycle-limited", "category", "niche", now);
        context.ParserLaunchRequests.Add(CompletedLaunch(launchMode, "cycle-limited", now));
        context.ParserProxyRuns.Add(run);
        context.ParserBatchSubmissions.Add(CompletedBatch(run, "cycle-limited:batch:0001"));
        context.ParserCurrentProductRows.Add(CurrentProduct("1001", "category", "niche"));
        await context.SaveChangesAsync();

        var service = new ParserProductPresenceReconciliationService(context);
        var result = await service.TryReconcileCycleAsync("cycle-limited", CancellationToken.None);

        Assert.False(result.Reconciled);
        Assert.Equal(ParserMarketplacePresenceStatuses.Active, await context.ParserCurrentProductRows.Select(x => x.MarketplacePresenceStatus).SingleAsync());
        Assert.Empty(await context.ParserProductPresenceEvents.ToListAsync());
    }

    [Fact]
    public async Task CycleWithPendingBatchDoesNotReconcileYet()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 7, 5, 10);
        var run = CompletedProxyRun("cycle-full", "category", "niche", now);
        var pendingBatch = new ParserBatchSubmission(
            run.ParserInstanceId,
            "cycle-full:batch:0002",
            run.SourceCategory,
            run.SourceSubcategory,
            run.ProxyKey,
            "complete_card_batch",
            "sha256:pending",
            now);
        pendingBatch.AttachRunMetadata(run.ParserCycleId, run.ExternalProxyRunId, run.Id, now);

        context.ParserLaunchRequests.Add(CompletedLaunch(ParserLaunchModes.FullAll, "cycle-full", now));
        context.ParserProxyRuns.Add(run);
        context.ParserBatchSubmissions.Add(pendingBatch);
        context.ParserCurrentProductRows.Add(CurrentProduct("1001", "category", "niche"));
        await context.SaveChangesAsync();

        var service = new ParserProductPresenceReconciliationService(context);
        var result = await service.TryReconcileCycleAsync("cycle-full", CancellationToken.None);

        Assert.False(result.Reconciled);
        Assert.Equal(ParserMarketplacePresenceStatuses.Active, await context.ParserCurrentProductRows.Select(x => x.MarketplacePresenceStatus).SingleAsync());
    }

    [Fact]
    public async Task FailedProxyRunScopeDoesNotMarkMissing()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 7, 5, 10);
        var failedRun = FailedProxyRun("cycle-full", "category", "niche", now);

        context.ParserLaunchRequests.Add(CompletedLaunch(ParserLaunchModes.FullAll, "cycle-full", now));
        context.ParserProxyRuns.Add(failedRun);
        context.ParserCurrentProductRows.Add(CurrentProduct("1001", "category", "niche"));
        await context.SaveChangesAsync();

        var service = new ParserProductPresenceReconciliationService(context);
        var result = await service.TryReconcileCycleAsync("cycle-full", CancellationToken.None);

        Assert.False(result.Reconciled);
        Assert.Equal(ParserMarketplacePresenceStatuses.Active, await context.ParserCurrentProductRows.Select(x => x.MarketplacePresenceStatus).SingleAsync());
    }

    private static ParserLaunchRequest CompletedLaunch(string mode, string parserCycleId, DateTime now)
    {
        var launch = new ParserLaunchRequest(
            Guid.NewGuid(),
            "parser-local-01",
            mode,
            mode == ParserLaunchModes.CheckProxy ? "proxy-1" : null,
            mode == ParserLaunchModes.FullAll ? null : 3,
            Guid.NewGuid(),
            now);
        launch.AssignParserCycle(parserCycleId);
        launch.MarkRunning(now.AddSeconds(1));
        launch.MarkCompleted(now.AddMinutes(5));
        return launch;
    }

    private static ParserProxyRun CompletedProxyRun(string cycleId, string category, string subcategory, DateTime now)
    {
        var run = new ParserProxyRun(
            "parser-local-01",
            $"{cycleId}:proxy-1:{category}:{subcategory}",
            cycleId,
            ParserProxyRunCycleKinds.Production,
            "proxy-1",
            category,
            subcategory,
            300,
            300,
            "203.0.113.10",
            null,
            "valid",
            now);
        run.Complete(300, 300, now.AddMinutes(2));
        return run;
    }

    private static ParserProxyRun FailedProxyRun(string cycleId, string category, string subcategory, DateTime now)
    {
        var run = new ParserProxyRun(
            "parser-local-01",
            $"{cycleId}:proxy-1:{category}:{subcategory}",
            cycleId,
            ParserProxyRunCycleKinds.Production,
            "proxy-1",
            category,
            subcategory,
            300,
            0,
            "203.0.113.10",
            null,
            "valid",
            now);
        run.Fail(300, 0, "failed", now.AddMinutes(2));
        return run;
    }

    private static ParserBatchSubmission CompletedBatch(ParserProxyRun run, string externalBatchId)
    {
        var batch = new ParserBatchSubmission(
            run.ParserInstanceId,
            externalBatchId,
            run.SourceCategory,
            run.SourceSubcategory,
            run.ProxyKey,
            "complete_card_batch",
            $"sha256:{externalBatchId}",
            run.StartedAtUtc);
        batch.AttachRunMetadata(run.ParserCycleId, run.ExternalProxyRunId, run.Id, run.StartedAtUtc);
        batch.MarkCompleted(run.StartedAtUtc.AddMinutes(1));
        return batch;
    }

    private static ParserCurrentProductRow CurrentProduct(string wbProductId, string category, string subcategory)
    {
        var row = new ParserProductRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            $"hash-{wbProductId}",
            1,
            "wildberries",
            "products-run",
            Utc(2026, 7, 1, 10),
            category,
            subcategory,
            null,
            "12354108",
            wbProductId,
            null,
            $"Product {wbProductId}",
            null,
            10,
            "Brand",
            20,
            "Seller",
            110,
            100,
            95,
            10,
            5,
            5,
            4.9m,
            11,
            "wb",
            null,
            1,
            $"root-{wbProductId}",
            1,
            2,
            null);
        return new ParserCurrentProductRow(
            row,
            new ProductGroupHashes(
                $"identity-{wbProductId}",
                $"price-{wbProductId}",
                $"stock-{wbProductId}",
                $"rating-{wbProductId}",
                $"reviews-{wbProductId}",
                $"media-{wbProductId}",
                $"seller-{wbProductId}"),
            Utc(2026, 7, 1, 10));
    }

    private static DateTime Utc(int year, int month, int day, int hour) =>
        DateTime.SpecifyKind(new DateTime(year, month, day, hour, 0, 0), DateTimeKind.Utc);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-product-presence-{Guid.NewGuid()}")
            .Options;
        return new ParserProductPresenceTestDbContext(options);
    }

    private sealed class ParserProductPresenceTestDbContext : ApplicationDbContext
    {
        public ParserProductPresenceTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(Role),
                typeof(ParserLaunchRequest),
                typeof(ParserProxyRun),
                typeof(ParserBatchSubmission),
                typeof(ParserCurrentProductRow),
                typeof(ParserProductPresenceEvent)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (!allowedTypes.Contains(entityType))
                    modelBuilder.Ignore(entityType);
            }

            modelBuilder.Entity<Role>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserLaunchRequest>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserProxyRun>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserBatchSubmission>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductRow>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserProductPresenceEvent>().HasKey(x => x.Id);
        }
    }
}
