using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserBatches;

public sealed class ParserProxyRunServiceTests
{
    [Fact]
    public async Task StartAsync_rejects_proxy_key_outside_active_launch_contract()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        var config = new ParserInstanceConfiguration(
            "parser-local-01",
            "local",
            ParserInstanceHostKinds.Local,
            now);
        var proxy = new ParserProxy(
            "46.161.30.248",
            19118,
            23597,
            "login",
            "encrypted",
            now);
        context.ParserInstanceConfigurations.Add(config);
        context.ParserProxies.Add(proxy);
        context.ParserInstanceProxyAssignments.Add(new ParserInstanceProxyAssignment(config.Id, proxy.Id, now));
        context.ParserProxyNicheAssignments.Add(new ParserProxyNicheAssignment(
            proxy.Id,
            10012,
            "Красота",
            "Органическая косметика",
            "Красота / Органическая косметика",
            "menu_redirect_subject_v2_10012 органическая косметика",
            "Органическая косметика",
            true,
            now));
        var launch = new ParserLaunchRequest(
            config.Id,
            config.ParserInstanceId,
            ParserLaunchModes.CheckProxy,
            proxy.Key,
            3,
            Guid.NewGuid(),
            now);
        launch.MarkRunning(now.AddSeconds(1));
        context.ParserLaunchRequests.Add(launch);
        await context.SaveChangesAsync();
        var service = new ParserProxyRunService(context);

        var result = await service.StartAsync(
            new ParserProxyRunStartRequest(
                config.ParserInstanceId,
                "external-run-1",
                launch.ParserCycleId,
                ParserProxyRunCycleKinds.Diagnostic,
                "proxy-3",
                "Красота",
                "Органическая косметика",
                300,
                0),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await context.ParserProxyRuns.CountAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-proxy-runs-{Guid.NewGuid()}")
            .Options;

        return new ParserProxyRunTestDbContext(options);
    }

    private sealed class ParserProxyRunTestDbContext : ApplicationDbContext
    {
        public ParserProxyRunTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(ParserInstance),
                typeof(ParserInstanceConfiguration),
                typeof(ParserInstanceProxyAssignment),
                typeof(ParserLaunchRequest),
                typeof(ParserProxy),
                typeof(ParserProxyNicheAssignment),
                typeof(ParserProxyRun)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (!allowedTypes.Contains(entityType))
                    modelBuilder.Ignore(entityType);
            }

            foreach (var entityType in allowedTypes)
            {
                modelBuilder.Entity(entityType).HasKey("Id");
                modelBuilder.Entity(entityType).Property<Guid>("Id").ValueGeneratedNever();
            }

            modelBuilder.Entity<ParserInstanceConfiguration>()
                .HasMany(x => x.ProxyAssignments)
                .WithOne(x => x.ParserInstanceConfiguration)
                .HasForeignKey(x => x.ParserInstanceConfigurationId);
            modelBuilder.Entity<ParserProxy>()
                .HasOne(x => x.Assignment)
                .WithOne(x => x.Proxy)
                .HasForeignKey<ParserProxyNicheAssignment>(x => x.ProxyId);
            modelBuilder.Entity<ParserInstanceProxyAssignment>()
                .HasOne(x => x.Proxy)
                .WithMany()
                .HasForeignKey(x => x.ProxyId);
        }
    }
}
