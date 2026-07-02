using System.Text.Json;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserBatches;

public sealed class ParserAdminMonitoringServiceTests
{
    [Fact]
    public async Task GetInstancesAsync_returns_instances_for_admin()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        await SubmitAsync(context, "batch-1");
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetInstancesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var instance = Assert.Single(result.Value!);
        Assert.Equal("parser-1", instance.ParserInstanceId);
        var proxy = Assert.Single(instance.Proxies);
        Assert.Equal("local-proxy", proxy.ProxyKey);
    }

    [Fact]
    public async Task GetBatchesAsync_returns_forbidden_for_non_admin()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context, "Manager");
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(role.Id));

        var result = await service.GetBatchesAsync(null, 1, 50, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task RetryBatchAsync_requeues_failed_batch_and_writes_event()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        await SubmitAsync(context, "batch-1");
        var batch = await context.ParserBatchSubmissions.SingleAsync();
        batch.MarkFailedFinal("broken payload", DateTime.UtcNow);
        await context.SaveChangesAsync();
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RetryBatchAsync(batch.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParserBatchStatuses.Queued, result.Value!.Status);
        Assert.Equal(ParserBatchStatuses.Queued, await context.ParserBatchSubmissions.Select(x => x.Status).SingleAsync());
        Assert.Null(await context.ParserBatchSubmissions.Select(x => x.Error).SingleAsync());
        Assert.Contains(
            await context.ParserBatchSubmissionEvents.Select(x => x.EventType).ToListAsync(),
            x => x == "retry_requested");
    }

    [Fact]
    public async Task ProxyRunLifecycle_is_idempotent_and_updates_progress()
    {
        await using var context = CreateContext();
        var service = new ParserProxyRunService(context);

        var first = await service.StartAsync(
            new ParserProxyRunStartRequest(
                "parser-1",
                "run-1",
                null,
                null,
                "proxy-1",
                "category",
                "niche",
                100,
                0),
            CancellationToken.None);
        var duplicate = await service.StartAsync(
            new ParserProxyRunStartRequest(
                "parser-1",
                "run-1",
                null,
                null,
                "proxy-1",
                "category",
                "niche",
                300,
                5),
            CancellationToken.None);
        var progress = await service.UpdateProgressAsync(
            "run-1",
            new ParserProxyRunProgressRequest("parser-1", 300, 20),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(duplicate.IsSuccess);
        Assert.True(progress.IsSuccess);
        Assert.Equal(first.Value!.Id, duplicate.Value!.Id);
        Assert.Equal(20, progress.Value!.DownloadedProductsCount);
        Assert.Equal(300, progress.Value!.PlannedProductsCount);
        Assert.Equal(1, await context.ParserProxyRuns.CountAsync());
    }

    [Fact]
    public async Task ProxyRunLifecycle_rejects_second_running_run_for_same_proxy()
    {
        await using var context = CreateContext();
        var service = new ParserProxyRunService(context);

        var first = await service.StartAsync(
            new ParserProxyRunStartRequest(
                "parser-1",
                "cycle-1:proxy-1:category:niche",
                "cycle-1",
                ParserProxyRunCycleKinds.Production,
                "proxy-1",
                "category",
                "niche",
                300,
                0),
            CancellationToken.None);
        var second = await service.StartAsync(
            new ParserProxyRunStartRequest(
                "parser-1",
                "cycle-2:proxy-1:category:niche",
                "cycle-2",
                ParserProxyRunCycleKinds.Production,
                "proxy-1",
                "category",
                "niche",
                300,
                0),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(ServiceErrorType.Conflict, second.Error?.Type);
        Assert.Equal(1, await context.ParserProxyRuns.CountAsync());
    }

    [Fact]
    public async Task GetJournalAsync_returns_completed_and_failed_proxy_runs()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var lifecycle = new ParserProxyRunService(context);
        await lifecycle.StartAsync(
            new ParserProxyRunStartRequest("parser-1", "run-1", null, null, "proxy-1", "category", "niche", 100, 10),
            CancellationToken.None);
        await lifecycle.FinishAsync(
            "run-1",
            new ParserProxyRunFinishRequest("parser-1", ParserProxyRunStatuses.Completed, 100, 100, null),
            CancellationToken.None);
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetJournalAsync(1, 50, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!);
        Assert.Equal("proxy-1", item.ProxyKey);
        Assert.Equal(ParserProxyRunStatuses.Completed, item.Status);
        Assert.Equal(100, item.DownloadedProductsCount);
    }

    [Fact]
    public async Task GetInstancesAsync_shows_latest_actual_cycle_without_preferring_full_old_cycle()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        context.ParserInstances.Add(new ParserInstance("parser-1", "parser-1", DateTime.UtcNow.AddMinutes(-20)));
        var fullCycleStartedAt = DateTime.UtcNow.AddMinutes(-10);
        var oldRun1 = new ParserProxyRun(
            "parser-1",
            "cycle-full:proxy-1:category:niche-1",
            "cycle-full",
            ParserProxyRunCycleKinds.Production,
            "proxy-1",
            "category",
            "niche-1",
            100,
            100,
            null,
            null,
            null,
            fullCycleStartedAt);
        oldRun1.Complete(100, 100, fullCycleStartedAt.AddMinutes(1));
        context.ParserProxyRuns.Add(oldRun1);
        var oldRun2 = new ParserProxyRun(
            "parser-1",
            "cycle-full:proxy-2:category:niche-2",
            "cycle-full",
            ParserProxyRunCycleKinds.Production,
            "proxy-2",
            "category",
            "niche-2",
            100,
            100,
            null,
            null,
            null,
            fullCycleStartedAt.AddSeconds(1));
        oldRun2.Complete(100, 100, fullCycleStartedAt.AddMinutes(1));
        context.ParserProxyRuns.Add(oldRun2);
        var oldRun3 = new ParserProxyRun(
            "parser-1",
            "cycle-full:proxy-3:category:niche-3",
            "cycle-full",
            ParserProxyRunCycleKinds.Production,
            "proxy-3",
            "category",
            "niche-3",
            100,
            100,
            null,
            null,
            null,
            fullCycleStartedAt.AddSeconds(2));
        oldRun3.Complete(100, 100, fullCycleStartedAt.AddMinutes(1));
        context.ParserProxyRuns.Add(oldRun3);
        context.ParserProxyRuns.Add(new ParserProxyRun(
            "parser-1",
            "cycle-single:proxy-1:category:niche-1",
            "cycle-single",
            ParserProxyRunCycleKinds.Production,
            "proxy-1",
            "category",
            "niche-1",
            100,
            100,
            null,
            null,
            null,
            DateTime.UtcNow));
        await context.SaveChangesAsync();
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetInstancesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var instance = Assert.Single(result.Value!);
        Assert.Equal(1, instance.TotalProxiesCount);
        var proxy = Assert.Single(instance.Proxies);
        Assert.Equal("proxy-1", proxy.ProxyKey);
        Assert.Equal("cycle-single", proxy.ParserCycleId);
    }

    private static async Task<Role> SeedRoleAsync(ApplicationDbContext context, string name)
    {
        var now = DateTime.UtcNow;
        var role = new Role(name, null, now, now);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static async Task SubmitAsync(ApplicationDbContext context, string externalBatchId)
    {
        var queue = new ParserBatchQueueService(context);
        using var document = JsonDocument.Parse("""{"items":[{"wbProductId":"1"}]}""");
        var result = await queue.SubmitAsync(
            new ParserBatchSubmitRequest(
                "parser-1",
                externalBatchId,
                "РўРѕРІР°СЂС‹ РґР»СЏ РґРѕРјР°",
                "РљРѕРІСЂРёРєРё РґР»СЏ РІР°РЅРЅРѕР№",
                "local-proxy",
                "full",
                null,
                document.RootElement.Clone()),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
        context.ParserProxyRuns.Add(new ParserProxyRun(
            "parser-1",
            $"proxy-run-{externalBatchId}",
            "local-proxy",
            "test-category",
            "test-subcategory",
            10,
            10,
            "203.0.113.10",
            "token...ref",
            "valid",
            DateTime.UtcNow));
        await context.SaveChangesAsync();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-admin-monitoring-{Guid.NewGuid()}")
            .Options;

        return new ParserAdminMonitoringTestDbContext(options);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid? roleId)
        {
            RoleId = roleId;
        }

        public bool IsAuthenticated => RoleId.HasValue;
        public Guid? UserId => Guid.NewGuid();
        public Guid? RoleId { get; }
        public int? SessionId => 1;
    }

    private sealed class ParserAdminMonitoringTestDbContext : ApplicationDbContext
    {
        public ParserAdminMonitoringTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(Role),
                typeof(ParserInstance),
                typeof(ParserProxyRun),
                typeof(ParserNicheAssignment),
                typeof(ParserPriceSplitJob),
                typeof(ParserPriceSplitRange),
                typeof(ParserBatchSubmission),
                typeof(ParserBatchArtifact),
                typeof(ParserBatchSubmissionEvent)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (!allowedTypes.Contains(entityType))
                    modelBuilder.Ignore(entityType);
            }

            modelBuilder.Entity<Role>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserInstance>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserProxyRun>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserNicheAssignment>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserPriceSplitJob>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserPriceSplitRange>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserBatchSubmission>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserBatchArtifact>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserBatchSubmissionEvent>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
