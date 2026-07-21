using AshmesMarketplaces.Application.Auth.Dtos;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Auth.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.Auth;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_MatchesRegisteredEmailCaseInsensitively()
    {
        await using var context = CreateContext();
        var role = new Role("Manager", null, DateTime.UtcNow, DateTime.UtcNow);
        context.Roles.Add(role);

        var passwordHashService = new PasswordHashService();
        var user = new User(
            role.Id,
            "seller@example.test",
            passwordHashService.HashPassword("correct-password"),
            "seller@example.test",
            "not-provided",
            1,
            DateTime.UtcNow,
            DateTime.UtcNow);
        var workspace = new Workspace(null, "Первое пространство", null, null, 1, DateTime.UtcNow, DateTime.UtcNow);
        context.Users.Add(user);
        context.Workspaces.Add(workspace);
        context.UserWorkspaces.Add(new UserWorkspace(user.Id, workspace.Id, role.Id));
        await context.SaveChangesAsync();

        var service = CreateService(context, passwordHashService);

        var result = await service.LoginAsync(
            new LoginRequest
            {
                Login = "Seller@Example.Test",
                Password = "correct-password"
            },
            ipAddress: null,
            userAgent: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value!.User.Id);
    }

    [Fact]
    public async Task RegisterAsync_CreatesDefaultWorkspaceWithReadableName()
    {
        await using var context = CreateContext();
        var service = CreateService(context, new PasswordHashService());

        var result = await service.RegisterAsync(
            new RegisterRequest
            {
                Email = "new-user@example.test",
                Password = "correct-password"
            },
            ipAddress: null,
            userAgent: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var workspace = Assert.Single(result.Value!.User.Workspaces);
        Assert.Equal("Первое пространство", workspace.WorkspaceName);
        Assert.Contains(context.Roles, role => role.Name == "Manager");
    }

    private static AuthService CreateService(
        ApplicationDbContext context,
        IPasswordHashService passwordHashService)
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-at-least-32-bytes-long",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 30
        });

        return new AuthService(
            context,
            passwordHashService,
            new RefreshTokenService(),
            new AccessTokenService(jwtOptions),
            new FixedUserAnalysisScheduleService(),
            new TestCurrentUser(null),
            jwtOptions);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AuthTestDbContext(options);
    }

    private sealed class FixedUserAnalysisScheduleService : IUserAnalysisScheduleService
    {
        public Task<AuthAnalysisScheduleResponse> EnsureScheduleAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuthAnalysisScheduleResponse(
                "Europe/Moscow",
                "03:00",
                "03:10",
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(1).AddMinutes(10),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));
        }
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

    private sealed class AuthTestDbContext : ApplicationDbContext
    {
        public AuthTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(Role),
                typeof(RolePermission),
                typeof(Permission),
                typeof(User),
                typeof(Session),
                typeof(Workspace),
                typeof(UserWorkspace)
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
            modelBuilder.Entity<RolePermission>(builder =>
            {
                builder.HasKey(x => new { x.IdRole, x.IdPermission });
            });
            modelBuilder.Entity<Permission>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<User>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<Session>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<Workspace>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<UserWorkspace>(builder =>
            {
                builder.HasKey(x => new { x.IdUser, x.IdWorkspace });
            });
        }
    }
}
