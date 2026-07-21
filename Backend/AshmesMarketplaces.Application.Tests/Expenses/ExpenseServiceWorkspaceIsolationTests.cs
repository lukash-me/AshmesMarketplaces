using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Expenses.Dtos;
using AshmesMarketplaces.Application.Expenses.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Finance;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.Expenses;

public sealed class ExpenseServiceWorkspaceIsolationTests
{
    [Fact]
    public async Task GetListAsync_RequiresWorkspaceId()
    {
        await using var context = CreateContext();
        var currentUser = new TestCurrentUser(Guid.NewGuid());
        var service = new ExpenseService(context, currentUser);

        var result = await service.GetListAsync(new ExpenseListQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.BadRequest, result.Error?.Type);
        Assert.Equal("Workspace id is required.", result.Error?.Message);
    }

    [Fact]
    public async Task GetListAsync_ReturnsOnlyExpensesFromAccessibleWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ExpenseService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.GetListAsync(
            new ExpenseListQuery { IdWorkspace = fixture.WorkspaceA.Id, Page = 1, PageSize = 50 },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("Workspace A expense", item.Name);
        Assert.Equal(fixture.WorkspaceA.Id, item.IdWorkspace);
    }

    [Fact]
    public async Task GetListAsync_ForbidsForeignWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ExpenseService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.GetListAsync(
            new ExpenseListQuery { IdWorkspace = fixture.WorkspaceB.Id },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task CreateAsync_UsesCurrentUserAsCreator()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ExpenseService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.CreateAsync(
            new CreateExpenseRequest
            {
                IdWorkspace = fixture.WorkspaceA.Id,
                IdCreator = fixture.UserB.Id,
                Name = "Created by current user",
                Cost = 100,
                Status = 0,
                StatusKey = "planned",
                DateCreate = DateTime.UtcNow,
                DateUpdate = DateTime.UtcNow
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.UserA.Id, result.Value!.IdCreator);
    }

    [Fact]
    public async Task UpdateAsync_ForbidsMovingExpenseToForeignWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ExpenseService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.UpdateAsync(
            fixture.ExpenseA.Id,
            new UpdateExpenseRequest
            {
                IdWorkspace = fixture.WorkspaceB.Id,
                IdCategory = fixture.ExpenseA.IdCategory,
                IdCreator = fixture.UserB.Id,
                Name = "Moved",
                Cost = 100,
                Status = 0,
                StatusKey = "planned",
                DateCreate = fixture.ExpenseA.DateCreate,
                DateUpdate = DateTime.UtcNow
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ExpenseTestDbContext(options);
    }

    private static Fixture SeedTwoWorkspaces(ApplicationDbContext context)
    {
        var now = DateTime.UtcNow;
        var role = new Role("Manager", null, now, now);
        var userA = new User(role.Id, "user-a@example.test", "hash", "user-a@example.test", "not-provided", 1, now, now);
        var userB = new User(role.Id, "user-b@example.test", "hash", "user-b@example.test", "not-provided", 1, now, now);
        var workspaceA = new Workspace(null, "Workspace A", null, null, 1, now, now);
        var workspaceB = new Workspace(null, "Workspace B", null, null, 1, now, now);
        var expenseA = new Expense(workspaceA.Id, null, userA.Id, null, "Workspace A expense", null, 100, 0, null, now, now);
        var expenseB = new Expense(workspaceB.Id, null, userB.Id, null, "Workspace B expense", null, 200, 0, null, now, now);

        context.Roles.Add(role);
        context.Users.AddRange(userA, userB);
        context.Workspaces.AddRange(workspaceA, workspaceB);
        context.UserWorkspaces.AddRange(
            new UserWorkspace(userA.Id, workspaceA.Id, role.Id),
            new UserWorkspace(userB.Id, workspaceB.Id, role.Id));
        context.Expenses.AddRange(expenseA, expenseB);
        context.SaveChanges();

        return new Fixture(userA, userB, workspaceA, workspaceB, expenseA);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid? userId)
        {
            UserId = userId;
        }

        public bool IsAuthenticated => UserId.HasValue;
        public Guid? UserId { get; }
        public Guid? RoleId => null;
        public int? SessionId => null;
    }

    private sealed record Fixture(
        User UserA,
        User UserB,
        Workspace WorkspaceA,
        Workspace WorkspaceB,
        Expense ExpenseA);

    private sealed class ExpenseTestDbContext : ApplicationDbContext
    {
        public ExpenseTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(Role),
                typeof(User),
                typeof(Workspace),
                typeof(UserWorkspace),
                typeof(Expense),
                typeof(ExpenseCategory)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (!allowedTypes.Contains(entityType))
                    modelBuilder.Ignore(entityType);
            }

            ConfigureRole(modelBuilder);
            ConfigureUser(modelBuilder);
            ConfigureWorkspace(modelBuilder);
            ConfigureUserWorkspace(modelBuilder);
            ConfigureExpense(modelBuilder);
            ConfigureExpenseCategory(modelBuilder);
        }

        private static void ConfigureRole(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }

        private static void ConfigureUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }

        private static void ConfigureWorkspace(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Workspace>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }

        private static void ConfigureUserWorkspace(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserWorkspace>(builder =>
            {
                builder.HasKey(x => new { x.IdUser, x.IdWorkspace });
            });
        }

        private static void ConfigureExpense(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Expense>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }

        private static void ConfigureExpenseCategory(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExpenseCategory>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
