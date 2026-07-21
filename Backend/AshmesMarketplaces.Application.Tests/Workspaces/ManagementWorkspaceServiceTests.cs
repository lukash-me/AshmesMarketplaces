using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Workspaces.Dtos;
using AshmesMarketplaces.Application.Workspaces.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.Workspaces;

public sealed class ManagementWorkspaceServiceTests
{
    [Fact]
    public async Task GetListAsync_ReturnsOnlyCurrentUserWorkspacesWithMembers()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.GetListAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var workspace = Assert.Single(result.Value!);
        Assert.Equal(fixture.WorkspaceA.Id, workspace.Id);
        Assert.Equal("Workspace A", workspace.Name);
        Assert.Equal(2, workspace.Members.Count);
        Assert.Contains(workspace.Members, x => x.IdUser == fixture.UserA.Id && x.IsCurrentUser);
        Assert.Contains(workspace.Members, x => x.IdUser == fixture.UserC.Id && !x.IsCurrentUser);
    }

    [Fact]
    public async Task GetListAsync_RequiresAuthentication()
    {
        await using var context = CreateContext();
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(null));

        var result = await service.GetListAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Unauthorized, result.Error?.Type);
    }

    [Fact]
    public async Task CreateAsync_CreatesWorkspaceAndAddsCurrentUser()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.CreateAsync(
            new CreateManagementWorkspaceRequest("New workspace", "Fresh team space"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New workspace", result.Value!.Name);
        Assert.Contains(result.Value.Members, x => x.IdUser == fixture.UserA.Id && x.IsCurrentUser);
        Assert.True(await context.UserWorkspaces.AnyAsync(
            x => x.IdWorkspace == result.Value.Id && x.IdUser == fixture.UserA.Id));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAccessibleWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.UpdateAsync(
            fixture.WorkspaceA.Id,
            new UpdateManagementWorkspaceRequest("Updated workspace", "Updated description"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated workspace", result.Value!.Name);
        Assert.Equal("Updated description", result.Value.Description);
        var workspace = await context.Workspaces.SingleAsync(x => x.Id == fixture.WorkspaceA.Id);
        Assert.Equal("Updated workspace", workspace.Name);
        Assert.Equal("Updated description", workspace.Description);
    }

    [Fact]
    public async Task UpdateAsync_ForbidsForeignWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.UpdateAsync(
            fixture.WorkspaceB.Id,
            new UpdateManagementWorkspaceRequest("No access", null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task AddMemberAsync_AddsExistingUserByEmail()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.AddMemberAsync(
            fixture.WorkspaceA.Id,
            new AddManagementWorkspaceMemberRequest("user-b@example.test", fixture.Role.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.UserB.Id, result.Value!.IdUser);
        Assert.Equal(fixture.Role.Id, result.Value.IdRole);
        Assert.True(await context.UserWorkspaces.AnyAsync(
            x => x.IdWorkspace == fixture.WorkspaceA.Id && x.IdUser == fixture.UserB.Id));
    }

    [Fact]
    public async Task AddMemberAsync_ForbidsForeignWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.AddMemberAsync(
            fixture.WorkspaceB.Id,
            new AddManagementWorkspaceMemberRequest("user-c@example.test", fixture.Role.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsConflictForExistingMember()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.AddMemberAsync(
            fixture.WorkspaceA.Id,
            new AddManagementWorkspaceMemberRequest("user-c@example.test", fixture.Role.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Conflict, result.Error?.Type);
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsNotFoundForUnknownEmail()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.AddMemberAsync(
            fixture.WorkspaceA.Id,
            new AddManagementWorkspaceMemberRequest("missing@example.test", fixture.Role.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.NotFound, result.Error?.Type);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_UpdatesMemberRoleInsideAccessibleWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var newRole = new Role("Analyst", null, DateTime.UtcNow, DateTime.UtcNow);
        context.Roles.Add(newRole);
        await context.SaveChangesAsync();
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.UpdateMemberRoleAsync(
            fixture.WorkspaceA.Id,
            fixture.UserC.Id,
            new UpdateManagementWorkspaceMemberRoleRequest(newRole.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(newRole.Id, result.Value!.IdRole);
        Assert.Equal("Analyst", result.Value.RoleName);
        Assert.True(await context.UserWorkspaces.AnyAsync(
            x => x.IdWorkspace == fixture.WorkspaceA.Id
                && x.IdUser == fixture.UserC.Id
                && x.IdRole == newRole.Id));
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_ForbidsForeignWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.UpdateMemberRoleAsync(
            fixture.WorkspaceB.Id,
            fixture.UserB.Id,
            new UpdateManagementWorkspaceMemberRoleRequest(fixture.Role.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task DeleteMemberAsync_RemovesMemberFromAccessibleWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.DeleteMemberAsync(
            fixture.WorkspaceA.Id,
            fixture.UserC.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(await context.UserWorkspaces.AnyAsync(
            x => x.IdWorkspace == fixture.WorkspaceA.Id && x.IdUser == fixture.UserC.Id));
        Assert.True(await context.Users.AnyAsync(x => x.Id == fixture.UserC.Id));
    }

    [Fact]
    public async Task DeleteMemberAsync_ForbidsForeignWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.DeleteMemberAsync(
            fixture.WorkspaceB.Id,
            fixture.UserB.Id,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAccessibleWorkspaceAndMemberships()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.DeleteAsync(fixture.WorkspaceA.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(await context.Workspaces.AnyAsync(x => x.Id == fixture.WorkspaceA.Id));
        Assert.False(await context.UserWorkspaces.AnyAsync(x => x.IdWorkspace == fixture.WorkspaceA.Id));
        Assert.True(await context.Workspaces.AnyAsync(x => x.Id == fixture.WorkspaceB.Id));
        Assert.True(await context.Users.AnyAsync(x => x.Id == fixture.UserA.Id));
        Assert.True(await context.Users.AnyAsync(x => x.Id == fixture.UserC.Id));
    }

    [Fact]
    public async Task DeleteAsync_ForbidsForeignWorkspace()
    {
        await using var context = CreateContext();
        var fixture = SeedTwoWorkspaces(context);
        var service = new ManagementWorkspaceService(context, new TestCurrentUser(fixture.UserA.Id));

        var result = await service.DeleteAsync(fixture.WorkspaceB.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
        Assert.True(await context.Workspaces.AnyAsync(x => x.Id == fixture.WorkspaceB.Id));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ManagementWorkspaceTestDbContext(options);
    }

    private static Fixture SeedTwoWorkspaces(ApplicationDbContext context)
    {
        var now = DateTime.UtcNow;
        var role = new Role("Manager", null, now, now);
        var userA = new User(role.Id, "user-a@example.test", "hash", "user-a@example.test", "not-provided", 1, now, now);
        var userB = new User(role.Id, "user-b@example.test", "hash", "user-b@example.test", "not-provided", 1, now, now);
        var userC = new User(role.Id, "user-c@example.test", "hash", "user-c@example.test", "not-provided", 1, now, now);
        var workspaceA = new Workspace(null, "Workspace A", "A", null, 1, now, now);
        var workspaceB = new Workspace(null, "Workspace B", "B", null, 1, now, now);

        context.Roles.Add(role);
        context.Users.AddRange(userA, userB, userC);
        context.Workspaces.AddRange(workspaceA, workspaceB);
        context.UserWorkspaces.AddRange(
            new UserWorkspace(userA.Id, workspaceA.Id, role.Id),
            new UserWorkspace(userC.Id, workspaceA.Id, role.Id),
            new UserWorkspace(userB.Id, workspaceB.Id, role.Id));
        context.SaveChanges();

        return new Fixture(role, userA, userB, userC, workspaceA, workspaceB);
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
        Role Role,
        User UserA,
        User UserB,
        User UserC,
        Workspace WorkspaceA,
        Workspace WorkspaceB);

    private sealed class ManagementWorkspaceTestDbContext : ApplicationDbContext
    {
        public ManagementWorkspaceTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(Role),
                typeof(User),
                typeof(Workspace),
                typeof(UserWorkspace)
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
    }
}
