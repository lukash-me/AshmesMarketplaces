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

public sealed class ParserLaunchRequestServiceTests
{
    [Fact]
    public async Task RequestLaunchAsync_creates_limited_all_request_with_batch_limit()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var instance = await SeedLaunchableInstanceAsync(context);
        var service = new ParserLaunchRequestService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RequestLaunchAsync(
            instance.Id,
            new CreateParserLaunchRequest(ParserLaunchModes.LimitedAll, 3, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParserLaunchModes.LimitedAll, result.Value!.Mode);
        Assert.Equal(ParserLaunchRequestStatuses.Queued, result.Value.Status);
        Assert.Equal("parser-local-01", result.Value.ParserInstanceId);
        var stored = await context.ParserLaunchRequests.SingleAsync();
        Assert.Equal(3, stored.BatchLimit);
        Assert.Null(stored.ProxyKey);
    }

    [Fact]
    public async Task RequestLaunchAsync_creates_full_all_request_without_batch_limit()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var instance = await SeedLaunchableInstanceAsync(context);
        var service = new ParserLaunchRequestService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RequestLaunchAsync(
            instance.Id,
            new CreateParserLaunchRequest(ParserLaunchModes.FullAll, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParserLaunchModes.FullAll, result.Value!.Mode);
        var stored = await context.ParserLaunchRequests.SingleAsync();
        Assert.Null(stored.BatchLimit);
        Assert.Null(stored.ProxyKey);
    }

    [Fact]
    public async Task RequestLaunchAsync_rejects_check_proxy_without_proxy_key()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var instance = await SeedLaunchableInstanceAsync(context);
        var service = new ParserLaunchRequestService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RequestLaunchAsync(
            instance.Id,
            new CreateParserLaunchRequest(ParserLaunchModes.CheckProxy, 1, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.BadRequest, result.Error!.Type);
    }

    [Fact]
    public async Task RequestLaunchAsync_rejects_proxy_from_another_instance()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var instance = await SeedLaunchableInstanceAsync(context);
        var otherInstance = await SeedLaunchableInstanceAsync(context, "parser-local-02", "proxy-2");
        var service = new ParserLaunchRequestService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RequestLaunchAsync(
            instance.Id,
            new CreateParserLaunchRequest(ParserLaunchModes.CheckProxy, 1, "proxy-2"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.BadRequest, result.Error!.Type);
        Assert.NotEqual(instance.Id, otherInstance.Id);
    }

    [Fact]
    public async Task RequestLaunchAsync_rejects_second_active_launch_for_instance()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var instance = await SeedLaunchableInstanceAsync(context);
        var service = new ParserLaunchRequestService(context, new TestCurrentUser(adminRole.Id));
        var first = await service.RequestLaunchAsync(
            instance.Id,
            new CreateParserLaunchRequest(ParserLaunchModes.LimitedAll, 3, null),
            CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.RequestLaunchAsync(
            instance.Id,
            new CreateParserLaunchRequest(ParserLaunchModes.LimitedAll, 3, null),
            CancellationToken.None);

        Assert.False(second.IsSuccess);
        Assert.Equal(ServiceErrorType.Conflict, second.Error!.Type);
    }

    [Fact]
    public async Task RequestLaunchAsync_is_forbidden_for_non_admin()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context, "Manager");
        var instance = await SeedLaunchableInstanceAsync(context);
        var service = new ParserLaunchRequestService(context, new TestCurrentUser(role.Id));

        var result = await service.RequestLaunchAsync(
            instance.Id,
            new CreateParserLaunchRequest(ParserLaunchModes.LimitedAll, 3, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task CancelAsync_cancels_active_launch_before_proxy_run_exists()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var instance = await SeedLaunchableInstanceAsync(context);
        var service = new ParserLaunchRequestService(context, new TestCurrentUser(adminRole.Id));
        var created = await service.RequestLaunchAsync(
            instance.Id,
            new CreateParserLaunchRequest(ParserLaunchModes.CheckProxy, 3, "proxy-1"),
            CancellationToken.None);
        Assert.True(created.IsSuccess);

        var result = await service.CancelAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParserLaunchRequestStatuses.Cancelled, result.Value!.Status);
        var stored = await context.ParserLaunchRequests.SingleAsync();
        Assert.Equal(ParserLaunchRequestStatuses.Cancelled, stored.Status);
        Assert.Contains("отменен", stored.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelAsync_rejects_launch_after_proxy_run_exists()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var instance = await SeedLaunchableInstanceAsync(context);
        var service = new ParserLaunchRequestService(context, new TestCurrentUser(adminRole.Id));
        var created = await service.RequestLaunchAsync(
            instance.Id,
            new CreateParserLaunchRequest(ParserLaunchModes.CheckProxy, 3, "proxy-1"),
            CancellationToken.None);
        Assert.True(created.IsSuccess);
        var launch = await context.ParserLaunchRequests.SingleAsync();
        context.ParserProxyRuns.Add(new ParserProxyRun(
            launch.ParserInstanceId,
            "run-1",
            launch.ParserCycleId,
            ParserProxyRunCycleKinds.Diagnostic,
            "proxy-1",
            "category",
            "niche",
            300,
            0,
            null,
            null,
            null,
            DateTime.UtcNow));
        await context.SaveChangesAsync();

        var result = await service.CancelAsync(created.Value!.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task MarkCompletedAsync_marks_launch_failed_when_cycle_has_failed_proxy_run()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var instance = await SeedLaunchableInstanceAsync(context);
        var service = new ParserLaunchRequestService(context, new TestCurrentUser(adminRole.Id));
        var created = await service.RequestLaunchAsync(
            instance.Id,
            new CreateParserLaunchRequest(ParserLaunchModes.CheckProxy, 3, "proxy-1"),
            CancellationToken.None);
        Assert.True(created.IsSuccess);
        var launch = await context.ParserLaunchRequests.SingleAsync();
        var run = new ParserProxyRun(
            launch.ParserInstanceId,
            "run-1",
            launch.ParserCycleId,
            ParserProxyRunCycleKinds.Diagnostic,
            "proxy-1",
            "category",
            "niche",
            300,
            0,
            null,
            null,
            null,
            DateTime.UtcNow);
        run.Fail(300, 0, "Selected subcategory resolved to unexpected proxy.", DateTime.UtcNow);
        context.ParserProxyRuns.Add(run);
        await context.SaveChangesAsync();

        await service.MarkCompletedAsync(launch.Id, CancellationToken.None);

        var stored = await context.ParserLaunchRequests.SingleAsync();
        Assert.Equal(ParserLaunchRequestStatuses.Failed, stored.Status);
        Assert.Contains("proxy-run", stored.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("proxy-1", stored.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ParserInstanceConfiguration> SeedLaunchableInstanceAsync(
        ApplicationDbContext context,
        string parserInstanceId = "parser-local-01",
        string proxyKey = "proxy-1")
    {
        var now = DateTime.UtcNow;
        var proxy = new ParserProxy(proxyKey, "127.0.0.1", 8080, 1080, "user", "protected::secret", now);
        var nicheAssignment = new ParserProxyNicheAssignment(
            proxy.Id,
            8194,
            "Обувь",
            "Кеды и кроссовки",
            "Обувь / Мужская / Кеды и кроссовки",
            "menu_redirect_subject_v2_8194 мужские кеды и кроссовки",
            "Кеды и кроссовки",
            true,
            now);
        var instance = new ParserInstanceConfiguration(parserInstanceId, parserInstanceId, ParserInstanceHostKinds.Local, now);
        context.ParserProxies.Add(proxy);
        context.ParserProxyNicheAssignments.Add(nicheAssignment);
        context.ParserInstanceConfigurations.Add(instance);
        context.ParserInstanceProxyAssignments.Add(new ParserInstanceProxyAssignment(instance.Id, proxy.Id, now));
        await context.SaveChangesAsync();
        return instance;
    }

    private static async Task<Role> SeedRoleAsync(ApplicationDbContext context, string name)
    {
        var now = DateTime.UtcNow;
        var role = new Role(name, null, now, now);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-launch-requests-{Guid.NewGuid()}")
            .Options;

        return new ParserLaunchRequestTestDbContext(options);
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

    private sealed class ParserLaunchRequestTestDbContext : ApplicationDbContext
    {
        public ParserLaunchRequestTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(Role),
                typeof(ParserProxy),
                typeof(ParserProxyNicheAssignment),
                typeof(ParserInstanceConfiguration),
                typeof(ParserInstanceProxyAssignment),
                typeof(ParserProxyRun),
                typeof(ParserLaunchRequest)
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
}
