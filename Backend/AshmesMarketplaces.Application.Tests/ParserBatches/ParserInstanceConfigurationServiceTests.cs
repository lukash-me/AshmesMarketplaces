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

public sealed class ParserInstanceConfigurationServiceTests
{
    [Fact]
    public async Task CreateAsync_creates_instance_without_proxies()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);

        var result = await service.CreateAsync(
            new CreateParserInstanceConfigurationRequest("Локальный parser", []),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("parser-local-01", result.Value!.ParserInstanceId);
        Assert.Equal("Локальный parser", result.Value.DisplayName);
        Assert.Empty(result.Value.Proxies);
    }

    [Fact]
    public async Task CreateAsync_can_assign_multiple_proxies()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var proxy1 = SeedProxy(context, "proxy-1");
        var proxy2 = SeedProxy(context, "proxy-2");
        await context.SaveChangesAsync();
        var service = CreateService(context, adminRole.Id);

        var result = await service.CreateAsync(
            new CreateParserInstanceConfigurationRequest("Локальный parser", [proxy1.Id, proxy2.Id]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Proxies.Count);
        Assert.Equal(["proxy-1", "proxy-2"], result.Value.Proxies.Select(x => x.ProxyKey).ToArray());
    }

    [Fact]
    public async Task CreateAsync_rejects_proxy_assigned_to_another_active_instance()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var proxy = SeedProxy(context, "proxy-1");
        await context.SaveChangesAsync();
        var service = CreateService(context, adminRole.Id);
        var first = await service.CreateAsync(
            new CreateParserInstanceConfigurationRequest("Первый parser", [proxy.Id]),
            CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.CreateAsync(
            new CreateParserInstanceConfigurationRequest("Второй parser", [proxy.Id]),
            CancellationToken.None);

        Assert.False(second.IsSuccess);
        Assert.Equal(ServiceErrorType.Conflict, second.Error!.Type);
    }

    [Fact]
    public async Task GetAdminListAsync_is_forbidden_for_non_admin()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context, "Manager");
        var service = CreateService(context, role.Id);

        var result = await service.GetAdminListAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error!.Type);
    }

    private static ParserInstanceConfigurationService CreateService(ApplicationDbContext context, Guid roleId)
    {
        return new ParserInstanceConfigurationService(context, new TestCurrentUser(roleId));
    }

    private static async Task<Role> SeedRoleAsync(ApplicationDbContext context, string name)
    {
        var now = DateTime.UtcNow;
        var role = new Role(name, null, now, now);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static ParserProxy SeedProxy(ApplicationDbContext context, string key)
    {
        var now = DateTime.UtcNow;
        var proxy = new ParserProxy(key, "127.0.0.1", 8080, 1080, "user", "protected::secret", now);
        context.ParserProxies.Add(proxy);
        return proxy;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-instance-configurations-{Guid.NewGuid()}")
            .Options;

        return new ParserInstanceConfigurationTestDbContext(options);
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

    private sealed class ParserInstanceConfigurationTestDbContext : ApplicationDbContext
    {
        public ParserInstanceConfigurationTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
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
                typeof(ParserInstanceProxyAssignment)
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
        }
    }
}
