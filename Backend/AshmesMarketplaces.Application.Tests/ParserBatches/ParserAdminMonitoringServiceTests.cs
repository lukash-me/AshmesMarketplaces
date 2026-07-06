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
    public async Task ProxyRunLifecycle_preserves_wb_preflight_phase()
    {
        await using var context = CreateContext();
        var service = new ParserProxyRunService(context);

        var started = await service.StartAsync(
            new ParserProxyRunStartRequest(
                "parser-1",
                "run-1",
                null,
                null,
                "proxy-1",
                "category",
                "niche",
                0,
                0,
                Phase: ParserProxyRunPhases.WbPreflight),
            CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Equal(ParserProxyRunPhases.WbPreflight, started.Value!.Phase);
        Assert.Equal(ParserProxyRunPhases.WbPreflight, await context.ParserProxyRuns.Select(x => x.Phase).SingleAsync());
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
    public async Task GetJournalAsync_returns_created_and_updated_product_effect_counts()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var startedAt = new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc);
        var run = new ParserProxyRun(
            "parser-1",
            "cycle-1:proxy-1:category:niche",
            "cycle-1",
            ParserProxyRunCycleKinds.Production,
            "proxy-1",
            "category",
            "niche",
            300,
            300,
            "203.0.113.10",
            null,
            null,
            startedAt);
        run.Complete(300, 300, startedAt.AddMinutes(5));
        context.ParserProxyRuns.Add(run);
        var batch = CompletedBatch(run);
        context.ParserBatchSubmissions.Add(batch);
        context.ParserRunProductEffects.AddRange(
            new ParserRunProductEffect(run.Id, batch.Id, "1001", Guid.NewGuid(), ParserRunProductEffectTypes.Created, startedAt),
            new ParserRunProductEffect(run.Id, batch.Id, "1002", Guid.NewGuid(), ParserRunProductEffectTypes.Created, startedAt),
            new ParserRunProductEffect(run.Id, batch.Id, "1002", Guid.NewGuid(), ParserRunProductEffectTypes.Updated, startedAt),
            new ParserRunProductEffect(run.Id, batch.Id, "1003", Guid.NewGuid(), ParserRunProductEffectTypes.Updated, startedAt),
            new ParserRunProductEffect(run.Id, batch.Id, "1003", Guid.NewGuid(), ParserRunProductEffectTypes.Updated, startedAt));
        await context.SaveChangesAsync();
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetJournalAsync(1, 50, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!);
        Assert.True(item.HasProductEffectsLedger);
        Assert.Equal(2, item.CreatedProductsCount);
        Assert.Equal(1, item.UpdatedProductsCount);
    }

    [Fact]
    public async Task GetJournalAsync_marks_rows_without_product_effects_ledger()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var startedAt = new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc);
        var run = new ParserProxyRun(
            "parser-1",
            "cycle-1:proxy-1:category:niche",
            "cycle-1",
            ParserProxyRunCycleKinds.Production,
            "proxy-1",
            "category",
            "niche",
            300,
            300,
            null,
            null,
            null,
            startedAt);
        run.Complete(300, 300, startedAt.AddMinutes(5));
        context.ParserProxyRuns.Add(run);
        await context.SaveChangesAsync();
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetJournalAsync(1, 50, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!);
        Assert.False(item.HasProductEffectsLedger);
        Assert.Equal(0, item.CreatedProductsCount);
        Assert.Equal(0, item.UpdatedProductsCount);
    }

    [Fact]
    public async Task GetInstancesAsync_uses_last_activity_for_interrupted_runtime()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var startedAt = new DateTime(2026, 7, 3, 9, 0, 0, DateTimeKind.Utc);
        var lastActivityAt = startedAt.AddMinutes(10);
        var interruptedAt = startedAt.AddMinutes(30);
        var run = new ParserProxyRun(
            "parser-1",
            "cycle-1:proxy-1:category:niche",
            "cycle-1",
            ParserProxyRunCycleKinds.Production,
            "proxy-1",
            "category",
            "niche",
            300,
            100,
            null,
            null,
            null,
            startedAt);
        run.UpdateProgress(300, 100, lastActivityAt, ParserProxyRunPhases.Download);
        run.Interrupt(300, 100, "No parser log activity since 2026-07-03T09:10:00Z.", interruptedAt);
        context.ParserInstances.Add(new ParserInstance("parser-1", "parser-1", interruptedAt));
        context.ParserProxyRuns.Add(run);
        await context.SaveChangesAsync();
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetInstancesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var instance = Assert.Single(result.Value!);
        var proxy = Assert.Single(instance.Proxies);
        Assert.Equal(ParserProxyRunStatuses.Interrupted, proxy.Status);
        Assert.Equal(10, proxy.RuntimeMinutes);
        Assert.Equal(10, instance.RuntimeMinutes);
    }

    [Fact]
    public async Task GetInstancesAsync_shows_queued_check_proxy_launch_before_proxy_run_exists()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var now = new DateTime(2026, 7, 3, 18, 14, 58, DateTimeKind.Utc);
        var config = SeedConfiguredProxy(context, now);
        var launch = new ParserLaunchRequest(
            config.Id,
            config.ParserInstanceId,
            ParserLaunchModes.CheckProxy,
            "proxy-1",
            3,
            Guid.NewGuid(),
            now);
        context.ParserLaunchRequests.Add(launch);
        await context.SaveChangesAsync();
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetInstancesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var instance = Assert.Single(result.Value!);
        Assert.Equal(1, instance.RunningProxiesCount);
        var proxy = Assert.Single(instance.Proxies);
        Assert.Equal("proxy-1", proxy.ProxyKey);
        Assert.Equal(ParserLaunchRequestStatuses.Queued, proxy.Status);
        Assert.Equal(ParserLaunchRequestStatuses.Queued, proxy.Phase);
        Assert.Equal(300, proxy.PlannedProductsCount);
        Assert.Equal(0, proxy.DownloadedProductsCount);
    }

    [Fact]
    public async Task GetInstancesAsync_replaces_active_launch_projection_with_real_proxy_run()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var now = new DateTime(2026, 7, 3, 18, 14, 58, DateTimeKind.Utc);
        var config = SeedConfiguredProxy(context, now);
        var launch = new ParserLaunchRequest(
            config.Id,
            config.ParserInstanceId,
            ParserLaunchModes.CheckProxy,
            "proxy-1",
            3,
            Guid.NewGuid(),
            now);
        launch.MarkRunning(now.AddSeconds(5));
        context.ParserLaunchRequests.Add(launch);
        context.ParserProxyRuns.Add(new ParserProxyRun(
            config.ParserInstanceId,
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
            now.AddMinutes(2)));
        await context.SaveChangesAsync();
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetInstancesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var instance = Assert.Single(result.Value!);
        var proxy = Assert.Single(instance.Proxies);
        Assert.Equal(ParserProxyRunStatuses.Running, proxy.Status);
        Assert.Equal("cycle-1", proxy.ParserCycleId);
        Assert.Equal(300, proxy.PlannedProductsCount);
    }

    [Fact]
    public async Task GetInstancesAsync_shows_active_launch_over_previous_finished_proxy_run()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var now = new DateTime(2026, 7, 3, 18, 14, 58, DateTimeKind.Utc);
        var config = SeedConfiguredProxy(context, now);
        var previousRun = new ParserProxyRun(
            config.ParserInstanceId,
            "cycle-old:proxy-1:category:niche",
            "cycle-old",
            ParserProxyRunCycleKinds.Production,
            "proxy-1",
            "category",
            "niche",
            300,
            300,
            null,
            null,
            null,
            now.AddHours(-2));
        previousRun.Complete(300, 300, now.AddHours(-1));
        context.ParserProxyRuns.Add(previousRun);
        var launch = new ParserLaunchRequest(
            config.Id,
            config.ParserInstanceId,
            ParserLaunchModes.CheckProxy,
            "proxy-1",
            3,
            Guid.NewGuid(),
            now);
        context.ParserLaunchRequests.Add(launch);
        await context.SaveChangesAsync();
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetInstancesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var proxy = Assert.Single(Assert.Single(result.Value!).Proxies);
        Assert.Equal(ParserLaunchRequestStatuses.Queued, proxy.Status);
        Assert.Equal(ParserLaunchRequestStatuses.Queued, proxy.Phase);
        Assert.Equal(300, proxy.PlannedProductsCount);
        Assert.Equal(0, proxy.DownloadedProductsCount);
    }

    [Fact]
    public async Task GetInstancesAsync_ignores_completed_launch_for_configured_proxy()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var now = new DateTime(2026, 7, 3, 18, 14, 58, DateTimeKind.Utc);
        var config = SeedConfiguredProxy(context, now);
        var launch = new ParserLaunchRequest(
            config.Id,
            config.ParserInstanceId,
            ParserLaunchModes.CheckProxy,
            "proxy-1",
            3,
            Guid.NewGuid(),
            now);
        launch.MarkCompleted(now.AddMinutes(1));
        context.ParserLaunchRequests.Add(launch);
        await context.SaveChangesAsync();
        var service = new ParserAdminMonitoringService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetInstancesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var proxy = Assert.Single(Assert.Single(result.Value!).Proxies);
        Assert.Equal("configured", proxy.Status);
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

    private static ParserInstanceConfiguration SeedConfiguredProxy(ApplicationDbContext context, DateTime now)
    {
        var proxy = new ParserProxy("proxy-1", "203.0.113.10", 3128, 1080, "login", "encrypted", now);
        context.ParserProxies.Add(proxy);
        context.SaveChanges();
        context.ParserProxyNicheAssignments.Add(new ParserProxyNicheAssignment(
            proxy.Id,
            8137,
            "Женщинам",
            "Платья и сарафаны",
            "Женщинам / Платья и сарафаны",
            "menu_v3_8137 платье женские",
            "Платья и сарафаны",
            true,
            now));
        var config = new ParserInstanceConfiguration(
            "parser-local-01",
            "inst1",
            ParserInstanceHostKinds.Local,
            now);
        context.ParserInstanceConfigurations.Add(config);
        context.SaveChanges();
        context.ParserInstanceProxyAssignments.Add(new ParserInstanceProxyAssignment(config.Id, proxy.Id, now));
        return config;
    }

    private static async Task SubmitAsync(ApplicationDbContext context, string externalBatchId)
    {
        var queue = new ParserBatchQueueService(context);
        using var document = JsonDocument.Parse("""{"items":[{"wbProductId":"1"}]}""");
        var result = await queue.SubmitAsync(
            new ParserBatchSubmitRequest(
                "parser-1",
                externalBatchId,
                "Товары для дома",
                "Коврики для ванной",
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

    private static ParserBatchSubmission CompletedBatch(ParserProxyRun run)
    {
        var batch = new ParserBatchSubmission(
            run.ParserInstanceId,
            $"{run.ParserCycleId}:batch:0001",
            run.SourceCategory,
            run.SourceSubcategory,
            run.ProxyKey,
            "complete_card_batch",
            """{"items":[]}""",
            DateTime.UtcNow);
        batch.AttachRunMetadata(run.ParserCycleId, run.ExternalProxyRunId, run.Id, DateTime.UtcNow);
        batch.MarkCompleted(DateTime.UtcNow);
        return batch;
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
                typeof(ParserProxy),
                typeof(ParserProxyNicheAssignment),
                typeof(ParserInstanceConfiguration),
                typeof(ParserInstanceProxyAssignment),
                typeof(ParserLaunchRequest),
                typeof(ParserPriceSplitJob),
                typeof(ParserPriceSplitRange),
                typeof(ParserBatchSubmission),
                typeof(ParserBatchArtifact),
                typeof(ParserBatchSubmissionEvent),
                typeof(ParserRunProductEffect)
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
            modelBuilder.Entity<ParserProxy>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.HasOne(x => x.Assignment)
                    .WithOne(x => x.Proxy)
                    .HasForeignKey<ParserProxyNicheAssignment>(x => x.ProxyId);
            });
            modelBuilder.Entity<ParserProxyNicheAssignment>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserInstanceConfiguration>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.HasMany(x => x.ProxyAssignments)
                    .WithOne(x => x.ParserInstanceConfiguration)
                    .HasForeignKey(x => x.ParserInstanceConfigurationId);
            });
            modelBuilder.Entity<ParserInstanceProxyAssignment>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.HasOne(x => x.Proxy)
                    .WithMany()
                    .HasForeignKey(x => x.ProxyId);
            });
            modelBuilder.Entity<ParserLaunchRequest>(builder =>
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
            modelBuilder.Entity<ParserRunProductEffect>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
