using AshmesMarketplaces.Application.AdminCalculations.Services;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.AdminCalculations;

public sealed class AdminCalculationServiceTests
{
    [Fact]
    public async Task GetCalculationsAsync_exposes_product_availability_as_scheduled_manually_runnable()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        await SeedScheduleAsync(context, PublicAnalysisSchedule.ProductAvailabilityScheduleKey);
        var service = new AdminCalculationService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetCalculationsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var calculation = Assert.Single(result.Value!, x => x.ScheduleKey == PublicAnalysisSchedule.ProductAvailabilityScheduleKey);
        Assert.Equal("scheduled", calculation.ExecutionMode);
        Assert.True(calculation.CanRunManually);
        Assert.Null(calculation.ManualRunDisabledReason);
        Assert.Contains("Доступность товара", calculation.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCalculationsAsync_does_not_expose_retired_hot_products_schedule()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        await SeedScheduleAsync(context, PublicAnalysisSchedule.HotProductsScheduleKey);
        var service = new AdminCalculationService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetCalculationsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value!, x => x.ScheduleKey == PublicAnalysisSchedule.HotProductsScheduleKey);
    }

    [Fact]
    public async Task GetCalculationsAsync_exposes_parser_current_products_as_on_demand()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        await SeedScheduleAsync(context, PublicAnalysisSchedule.ParserCurrentProductsScheduleKey);
        var service = new AdminCalculationService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.GetCalculationsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var calculation = Assert.Single(result.Value!, x => x.ScheduleKey == PublicAnalysisSchedule.ParserCurrentProductsScheduleKey);
        Assert.Equal("on_demand", calculation.ExecutionMode);
        Assert.True(calculation.CanRunManually);
        Assert.Null(calculation.ManualRunDisabledReason);
        Assert.Contains("Top 700", calculation.Description, StringComparison.Ordinal);
        Assert.Contains("не обращается к WB", calculation.Details, StringComparison.Ordinal);
        Assert.Contains("не инициирует parser-запуск", calculation.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestManualRunAsync_creates_product_availability_request_without_changing_schedule_time()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var schedule = await SeedScheduleAsync(context, PublicAnalysisSchedule.ProductAvailabilityScheduleKey);
        var originalNextRunAtUtc = schedule.NextRunAtUtc;
        var service = new AdminCalculationService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RequestManualRunAsync(
            PublicAnalysisSchedule.ProductAvailabilityScheduleKey,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PublicAnalysisManualRunRequest.QueuedStatus, result.Value!.Status);
        var storedRequest = await context.PublicAnalysisManualRunRequests.SingleAsync();
        Assert.Equal(PublicAnalysisSchedule.ProductAvailabilityScheduleKey, storedRequest.ScheduleKey);
        var storedSchedule = await context.PublicAnalysisSchedules.SingleAsync();
        Assert.Equal(originalNextRunAtUtc, storedSchedule.NextRunAtUtc);
    }

    [Fact]
    public async Task RequestManualRunAsync_creates_parser_current_products_request_without_changing_schedule_time()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var schedule = await SeedScheduleAsync(context, PublicAnalysisSchedule.ParserCurrentProductsScheduleKey);
        var originalNextRunAtUtc = schedule.NextRunAtUtc;
        var service = new AdminCalculationService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RequestManualRunAsync(
            PublicAnalysisSchedule.ParserCurrentProductsScheduleKey,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PublicAnalysisManualRunRequest.QueuedStatus, result.Value!.Status);
        var storedRequest = await context.PublicAnalysisManualRunRequests.SingleAsync();
        Assert.Equal(PublicAnalysisSchedule.ParserCurrentProductsScheduleKey, storedRequest.ScheduleKey);
        var storedSchedule = await context.PublicAnalysisSchedules.SingleAsync();
        Assert.Equal(originalNextRunAtUtc, storedSchedule.NextRunAtUtc);
    }

    [Fact]
    public async Task RequestManualRunAsync_rejects_retired_hot_products_schedule()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        await SeedScheduleAsync(context, PublicAnalysisSchedule.HotProductsScheduleKey);
        var service = new AdminCalculationService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RequestManualRunAsync(
            PublicAnalysisSchedule.HotProductsScheduleKey,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.BadRequest, result.Error!.Type);
        Assert.Empty(context.PublicAnalysisManualRunRequests);
    }

    [Fact]
    public async Task RequestManualRunAsync_rejects_second_active_product_availability_request()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        await SeedScheduleAsync(context, PublicAnalysisSchedule.ProductAvailabilityScheduleKey);
        var service = new AdminCalculationService(context, new TestCurrentUser(adminRole.Id));
        var first = await service.RequestManualRunAsync(
            PublicAnalysisSchedule.ProductAvailabilityScheduleKey,
            CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.RequestManualRunAsync(
            PublicAnalysisSchedule.ProductAvailabilityScheduleKey,
            CancellationToken.None);

        Assert.False(second.IsSuccess);
        Assert.Equal(ServiceErrorType.Conflict, second.Error!.Type);
    }

    [Fact]
    public async Task RequestManualRunAsync_rejects_unsupported_schedule_key()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        await SeedScheduleAsync(context, PublicAnalysisSchedule.MarketIntelligenceScheduleKey);
        var service = new AdminCalculationService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RequestManualRunAsync(
            PublicAnalysisSchedule.MarketIntelligenceScheduleKey,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.BadRequest, result.Error!.Type);
    }

    private static async Task<PublicAnalysisSchedule> SeedScheduleAsync(ApplicationDbContext context, string scheduleKey)
    {
        var now = DateTime.UtcNow;
        var schedule = new PublicAnalysisSchedule(
            scheduleKey,
            "Europe/Moscow",
            new TimeOnly(5, 0),
            now.AddHours(8),
            now);
        context.PublicAnalysisSchedules.Add(schedule);
        await context.SaveChangesAsync();
        return schedule;
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
            .UseInMemoryDatabase($"admin-calculations-{Guid.NewGuid()}")
            .Options;

        return new AdminCalculationTestDbContext(options);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        private readonly Guid _userId = Guid.NewGuid();

        public TestCurrentUser(Guid? roleId)
        {
            RoleId = roleId;
        }

        public bool IsAuthenticated => RoleId.HasValue;
        public Guid? UserId => _userId;
        public Guid? RoleId { get; }
        public int? SessionId => 1;
    }

    private sealed class AdminCalculationTestDbContext : ApplicationDbContext
    {
        public AdminCalculationTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(Role),
                typeof(PublicAnalysisSchedule),
                typeof(PublicAnalysisManualRunRequest)
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
            modelBuilder.Entity<PublicAnalysisSchedule>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<PublicAnalysisManualRunRequest>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
