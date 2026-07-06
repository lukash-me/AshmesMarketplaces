using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserBatches;

public sealed class ParserLogMonitoringServiceTests
{
    [Fact]
    public void ParserLogReader_restores_progress_from_structured_log_events()
    {
        using var temp = new TempDirectory();
        var logDir = System.IO.Path.Combine(temp.Path, "cycle-1", "proxy-1");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(
            System.IO.Path.Combine(logDir, "stderr.log"),
            """
            2026-07-03 12:00:00.000 | INFO | PARSER_EVENT {"event":"range_final","timestampUtc":"2026-07-03T09:00:00Z","proxyKey":"proxy-1","phase":"ranges","rangeChecksCount":1,"finalRangesCount":1,"plannedProductsCount":0,"downloadedProductsCount":0}
            2026-07-03 12:01:00.000 | INFO | PARSER_EVENT {"event":"range_empty","timestampUtc":"2026-07-03T09:01:00Z","proxyKey":"proxy-1","phase":"ranges","rangeChecksCount":2,"finalRangesCount":1,"emptyRangesCount":1}
            2026-07-03 12:02:00.000 | INFO | PARSER_EVENT {"event":"split_completed","timestampUtc":"2026-07-03T09:02:00Z","proxyKey":"proxy-1","phase":"download","rangeChecksCount":2,"finalRangesCount":1,"emptyRangesCount":1,"plannedProductsCount":300}
            2026-07-03 12:03:00.000 | INFO | PARSER_EVENT {"event":"stream_batch_enqueued","timestampUtc":"2026-07-03T09:03:00Z","proxyKey":"proxy-1","phase":"download","downloadedProductsCount":100,"plannedProductsCount":300}
            """,
            System.Text.Encoding.UTF8);

        var reader = new ParserLogReader(new ParserLogMonitoringOptions { LogRoot = temp.Path.ToString() });

        var snapshot = reader.ReadProxyRun("cycle-1", "proxy-1");

        Assert.NotNull(snapshot);
        Assert.Equal("download", snapshot!.Phase);
        Assert.Equal(300, snapshot.PlannedProductsCount);
        Assert.Equal(100, snapshot.DownloadedProductsCount);
        Assert.Equal(2, snapshot.RangeChecksCount);
        Assert.Equal(1, snapshot.FinalRangesCount);
        Assert.Equal(1, snapshot.EmptyRangesCount);
        Assert.Equal(DateTime.Parse("2026-07-03T09:03:00Z").ToUniversalTime(), snapshot.LastLogAtUtc);
        Assert.True(snapshot.ProductsPerSecond > 0);
        Assert.True(snapshot.RangesPerSecond > 0);
    }

    [Fact]
    public async Task LogMonitoring_marks_running_run_interrupted_when_logs_are_stale()
    {
        await using var context = CreateContext();
        using var temp = new TempDirectory();
        var started = new DateTime(2026, 7, 3, 9, 0, 0, DateTimeKind.Utc);
        var run = new ParserProxyRun(
            "parser-1",
            "cycle-1:proxy-1:category:niche",
            "cycle-1",
            ParserProxyRunCycleKinds.Production,
            "proxy-1",
            "category",
            "niche",
            300,
            0,
            null,
            null,
            null,
            started,
            ParserProxyRunPhases.Download);
        context.ParserProxyRuns.Add(run);
        await context.SaveChangesAsync();

        var logDir = System.IO.Path.Combine(temp.Path, "cycle-1", "proxy-1");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(
            System.IO.Path.Combine(logDir, "stdout.log"),
            """PARSER_EVENT {"event":"stream_batch_enqueued","timestampUtc":"2026-07-03T09:05:00Z","proxyKey":"proxy-1","phase":"download","downloadedProductsCount":100,"plannedProductsCount":300}""",
            System.Text.Encoding.UTF8);

        var service = new ParserRunLogMonitoringService(
            context,
            new ParserLogReader(new ParserLogMonitoringOptions
            {
                LogRoot = temp.Path.ToString(),
                StaleAfterSeconds = 60
            }));

        await service.ProcessOnceAsync(new DateTime(2026, 7, 3, 9, 10, 30, DateTimeKind.Utc), CancellationToken.None);

        var stored = await context.ParserProxyRuns.SingleAsync();
        Assert.Equal(ParserProxyRunStatuses.Interrupted, stored.Status);
        Assert.Equal(ParserProxyRunPhases.Interrupted, stored.Phase);
        Assert.Equal(300, stored.PlannedProductsCount);
        Assert.Equal(100, stored.DownloadedProductsCount);
        Assert.Contains("No parser log activity since", stored.Error);
    }

    [Fact]
    public async Task LogMonitoring_closes_running_launch_when_last_proxy_run_is_interrupted()
    {
        await using var context = CreateContext();
        using var temp = new TempDirectory();
        var started = new DateTime(2026, 7, 3, 9, 0, 0, DateTimeKind.Utc);
        var launch = new ParserLaunchRequest(
            Guid.NewGuid(),
            "parser-1",
            ParserLaunchModes.CheckProxy,
            "proxy-1",
            3,
            Guid.NewGuid(),
            started);
        launch.MarkRunning(started);
        var run = new ParserProxyRun(
            "parser-1",
            "cycle-1:proxy-1:category:niche",
            "cycle-1",
            ParserProxyRunCycleKinds.Diagnostic,
            "proxy-1",
            "category",
            "niche",
            300,
            0,
            null,
            null,
            null,
            started,
            ParserProxyRunPhases.Download);
        context.ParserLaunchRequests.Add(launch);
        context.ParserProxyRuns.Add(run);
        await context.SaveChangesAsync();

        var logDir = System.IO.Path.Combine(temp.Path, "cycle-1", "proxy-1");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(
            System.IO.Path.Combine(logDir, "stdout.log"),
            """PARSER_EVENT {"event":"stream_batch_enqueued","timestampUtc":"2026-07-03T09:05:00Z","proxyKey":"proxy-1","phase":"download","downloadedProductsCount":100,"plannedProductsCount":300}""",
            System.Text.Encoding.UTF8);

        var service = new ParserRunLogMonitoringService(
            context,
            new ParserLogReader(new ParserLogMonitoringOptions
            {
                LogRoot = temp.Path.ToString(),
                StaleAfterSeconds = 60
            }));

        await service.ProcessOnceAsync(new DateTime(2026, 7, 3, 9, 10, 30, DateTimeKind.Utc), CancellationToken.None);

        var storedLaunch = await context.ParserLaunchRequests.SingleAsync();
        Assert.Equal(ParserLaunchRequestStatuses.Failed, storedLaunch.Status);
        Assert.Contains("interrupted", storedLaunch.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LogMonitoring_closes_stale_active_launch_when_no_proxy_runs_are_running()
    {
        await using var context = CreateContext();
        using var temp = new TempDirectory();
        var started = new DateTime(2026, 7, 3, 9, 0, 0, DateTimeKind.Utc);
        var interruptedAt = started.AddMinutes(30);
        var launch = new ParserLaunchRequest(
            Guid.NewGuid(),
            "parser-1",
            ParserLaunchModes.CheckProxy,
            "proxy-1",
            3,
            Guid.NewGuid(),
            started);
        launch.MarkRunning(started);
        var run = new ParserProxyRun(
            "parser-1",
            "cycle-1:proxy-1:category:niche",
            "cycle-1",
            ParserProxyRunCycleKinds.Diagnostic,
            "proxy-1",
            "category",
            "niche",
            300,
            100,
            null,
            null,
            null,
            started,
            ParserProxyRunPhases.Download);
        run.Interrupt(300, 100, "No parser log activity since 2026-07-03T09:05:00Z.", interruptedAt);
        context.ParserLaunchRequests.Add(launch);
        context.ParserProxyRuns.Add(run);
        await context.SaveChangesAsync();

        var service = new ParserRunLogMonitoringService(
            context,
            new ParserLogReader(new ParserLogMonitoringOptions
            {
                LogRoot = temp.Path.ToString(),
                StaleAfterSeconds = 60
            }));

        await service.ProcessOnceAsync(new DateTime(2026, 7, 3, 9, 40, 0, DateTimeKind.Utc), CancellationToken.None);

        var storedLaunch = await context.ParserLaunchRequests.SingleAsync();
        Assert.Equal(ParserLaunchRequestStatuses.Failed, storedLaunch.Status);
        Assert.Contains("no active proxy runs", storedLaunch.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-log-monitoring-{Guid.NewGuid()}")
            .Options;

        return new ParserLogMonitoringTestDbContext(options);
    }

    private sealed class ParserLogMonitoringTestDbContext : ApplicationDbContext
    {
        public ParserLogMonitoringTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
                modelBuilder.Ignore(entityType.ClrType);

            modelBuilder.Entity<ParserProxyRun>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserLaunchRequest>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"parser-log-monitoring-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
