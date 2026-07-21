using System.Reflection;
using AshmesMarketplaces.Application.ParserObservability.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserObservability;

public sealed class PublicParserObservedLogisticsRefreshServiceTests
{
    [Fact]
    public async Task NewProductsCalculationUsesAllProxyRunsInLatestCompletedCycle()
    {
        await using var context = CreateContext();
        var startedAtUtc = Utc(2026, 7, 3, 10);
        var run1 = CompletedRun("cycle-1", "proxy-1", startedAtUtc);
        var run2 = CompletedRun("cycle-1", "proxy-2", startedAtUtc.AddMinutes(1));
        var run3 = CompletedRun("cycle-1", "proxy-3", startedAtUtc.AddMinutes(2));
        context.ParserProxyRuns.AddRange(run1, run2, run3);
        AddCreatedProducts(context, run1, 1, 3);
        AddCreatedProducts(context, run2, 101, 3);
        AddCreatedProducts(context, run3, 201, 3);
        await context.SaveChangesAsync();

        var rows = await LoadNewProductObservationsAsync(context);

        Assert.Equal(9, rows.Count);
    }

    [Fact]
    public async Task NewProductsCalculationDeduplicatesWbProductIdAcrossProxyRuns()
    {
        await using var context = CreateContext();
        var run1 = CompletedRun("cycle-1", "proxy-1", Utc(2026, 7, 3, 10));
        var run2 = CompletedRun("cycle-1", "proxy-2", Utc(2026, 7, 3, 10, 1));
        context.ParserProxyRuns.AddRange(run1, run2);
        AddCreatedProduct(context, run1, "1001");
        AddCreatedProduct(context, run2, "1001");
        await context.SaveChangesAsync();

        var rows = await LoadNewProductObservationsAsync(context);

        Assert.Single(rows);
        Assert.Equal("1001", Text(rows[0], "WbProductId"));
    }

    [Fact]
    public async Task NewProductsCalculationIgnoresNewerCycleWhileProxyRunIsStillRunning()
    {
        await using var context = CreateContext();
        var olderRun = CompletedRun("cycle-old", "proxy-1", Utc(2026, 7, 3, 10));
        var runningRun = RunningRun("cycle-new", "proxy-1", Utc(2026, 7, 3, 11));
        context.ParserProxyRuns.AddRange(olderRun, runningRun);
        AddCreatedProduct(context, olderRun, "old-1");
        AddCreatedProduct(context, runningRun, "new-1");
        await context.SaveChangesAsync();

        var rows = await LoadNewProductObservationsAsync(context);

        Assert.Single(rows);
        Assert.Equal("old-1", Text(rows[0], "WbProductId"));
    }

    [Fact]
    public async Task NewProductsCalculationIncludesCreatedEffectsFromFailedAndInterruptedRuns()
    {
        await using var context = CreateContext();
        var failedRun = FailedRun("cycle-1", "proxy-1", Utc(2026, 7, 3, 10));
        var interruptedRun = InterruptedRun("cycle-1", "proxy-2", Utc(2026, 7, 3, 10, 1));
        context.ParserProxyRuns.AddRange(failedRun, interruptedRun);
        AddCreatedProduct(context, failedRun, "failed-1");
        AddCreatedProduct(context, interruptedRun, "interrupted-1");
        await context.SaveChangesAsync();

        var rows = await LoadNewProductObservationsAsync(context);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => Text(row, "WbProductId") == "failed-1");
        Assert.Contains(rows, row => Text(row, "WbProductId") == "interrupted-1");
    }

    private static async Task<IReadOnlyList<object>> LoadNewProductObservationsAsync(ApplicationDbContext context)
    {
        var service = new PublicParserObservedLogisticsRefreshService(context);
        var method = typeof(PublicParserObservedLogisticsRefreshService).GetMethod(
            "LoadNewProductObservationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task)method.Invoke(service, ["latest-logistics-run", CancellationToken.None])!;
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        return ((System.Collections.IEnumerable)result).Cast<object>().ToList();
    }

    private static ParserProxyRun CompletedRun(string cycleId, string proxyKey, DateTime startedAtUtc)
    {
        var run = RunningRun(cycleId, proxyKey, startedAtUtc);
        run.Complete(100, 100, startedAtUtc.AddMinutes(5));
        return run;
    }

    private static ParserProxyRun FailedRun(string cycleId, string proxyKey, DateTime startedAtUtc)
    {
        var run = RunningRun(cycleId, proxyKey, startedAtUtc);
        run.Fail(100, 100, "test failure after created effects", startedAtUtc.AddMinutes(5));
        return run;
    }

    private static ParserProxyRun InterruptedRun(string cycleId, string proxyKey, DateTime startedAtUtc)
    {
        var run = RunningRun(cycleId, proxyKey, startedAtUtc);
        run.Interrupt(100, 100, "test interruption after created effects", startedAtUtc.AddMinutes(5));
        return run;
    }

    private static ParserProxyRun RunningRun(string cycleId, string proxyKey, DateTime startedAtUtc) =>
        new(
            "parser-local-01",
            $"{cycleId}:{proxyKey}",
            cycleId,
            ParserProxyRunCycleKinds.Production,
            proxyKey,
            "category",
            "niche",
            100,
            0,
            null,
            null,
            null,
            startedAtUtc);

    private static void AddCreatedProducts(ApplicationDbContext context, ParserProxyRun run, int startId, int count)
    {
        for (var offset = 0; offset < count; offset++)
            AddCreatedProduct(context, run, (startId + offset).ToString());
    }

    private static void AddCreatedProduct(ApplicationDbContext context, ParserProxyRun run, string wbProductId)
    {
        var now = Utc(2026, 7, 3, 12);
        context.ParserRunProductEffects.Add(new ParserRunProductEffect(
            run.Id,
            Guid.NewGuid(),
            wbProductId,
            Guid.NewGuid(),
            ParserRunProductEffectTypes.Created,
            now));
        context.ParserCurrentProductLogistics.Add(new ParserCurrentProductLogistics(
            wbProductId,
            null,
            "category",
            "niche",
            $"hash-{wbProductId}",
            """{"sourceRegionDest":"12354108","totalQuantityObserved":42}""",
            now,
            $"batch-{wbProductId}"));
    }

    private static string? Text(object row, string propertyName) =>
        row.GetType().GetProperty(propertyName)!.GetValue(row)?.ToString();

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        DateTime.SpecifyKind(new DateTime(year, month, day, hour, minute, 0), DateTimeKind.Utc);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"public-parser-observed-logistics-{Guid.NewGuid()}")
            .Options;
        return new PublicParserObservedLogisticsTestDbContext(options);
    }

    private sealed class PublicParserObservedLogisticsTestDbContext : ApplicationDbContext
    {
        public PublicParserObservedLogisticsTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var keptTypes = new HashSet<Type>
            {
                typeof(ParserProxyRun),
                typeof(ParserRunProductEffect),
                typeof(ParserCurrentProductLogistics)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
            {
                if (!keptTypes.Contains(entityType.ClrType))
                    modelBuilder.Ignore(entityType.ClrType);
            }

            modelBuilder.Entity<ParserProxyRun>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserRunProductEffect>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserCurrentProductLogistics>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
