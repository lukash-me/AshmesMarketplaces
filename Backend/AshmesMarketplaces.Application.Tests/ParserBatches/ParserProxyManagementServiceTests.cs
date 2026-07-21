using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketplaceCategories.Dtos;
using AshmesMarketplaces.Application.MarketplaceCategories.Services;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserBatches;

public sealed class ParserProxyManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_creates_proxy_without_niche_for_admin()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);

        var result = await service.CreateAsync(
            new CreateParserProxyRequest("127.0.0.1", 8080, 1080, "user", "secret", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value!.Id.ToString("D"), result.Value.Key);
        Assert.True(result.Value.HasPassword);
        Assert.Null(result.Value.Assignment);
        var stored = await context.ParserProxies.SingleAsync();
        Assert.Equal(stored.Id.ToString("D"), stored.Key);
        Assert.NotEqual("secret", stored.EncryptedPassword);
    }

    [Fact]
    public async Task CreateAsync_can_assign_wb_leaf_niche()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);

        var result = await service.CreateAsync(
            new CreateParserProxyRequest("10.0.0.1", 8080, 1080, "user", "secret", 8194),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.Assignment);
        Assert.Equal(8194, result.Value.Assignment!.WbCategoryId);
        Assert.Equal("Обувь", result.Value.Assignment.SourceCategory);
        Assert.Equal("Кеды и кроссовки", result.Value.Assignment.SourceSubcategory);
        Assert.Equal("Обувь / Мужская / Кеды и кроссовки", result.Value.Assignment.SourcePath);
        Assert.Equal("мужские кеды и кроссовки", result.Value.Assignment.ParserSearchText);
        Assert.NotEqual(result.Value.Assignment.SearchQuery, result.Value.Assignment.ParserSearchText);
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_wb_category()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);

        var result = await service.CreateAsync(
            new CreateParserProxyRequest("10.0.0.1", 8080, 1080, "user", "secret", 999999),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.BadRequest, result.Error!.Type);
        Assert.Empty(context.ParserProxies);
    }

    [Fact]
    public async Task UpdateAsync_without_password_preserves_existing_password()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);
        var created = await service.CreateAsync(
            new CreateParserProxyRequest("10.0.0.1", 8080, 1080, "user", "secret", 8194),
            CancellationToken.None);
        var before = await context.ParserProxies.Select(x => x.EncryptedPassword).SingleAsync();

        var result = await service.UpdateAsync(
            created.Value!.Id,
            new UpdateParserProxyRequest("10.0.0.2", 8081, 1081, "user2", null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Assignment);
        var proxy = await context.ParserProxies.SingleAsync();
        Assert.Equal(before, proxy.EncryptedPassword);
        Assert.Equal("10.0.0.2", proxy.Ip);
    }

    [Fact]
    public async Task RuntimeAssignments_returns_only_active_proxies_for_requested_instance()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);
        await service.CreateAsync(
            new CreateParserProxyRequest("10.0.0.1", 8080, 1080, "user", "secret1", 8194),
            CancellationToken.None);
        await service.CreateAsync(
            new CreateParserProxyRequest("10.0.0.2", 8080, 1080, "user", "secret2", 8194),
            CancellationToken.None);

        var proxy1 = await context.ParserProxies.OrderBy(x => x.CreatedAtUtc).FirstAsync();
        var proxy2 = await context.ParserProxies.OrderBy(x => x.CreatedAtUtc).Skip(1).FirstAsync();
        var now = DateTime.UtcNow;
        var localInstance = new ParserInstanceConfiguration("parser-local-01", "Локальный parser", ParserInstanceHostKinds.Local, now);
        var secondInstance = new ParserInstanceConfiguration("parser-local-02", "Второй parser", ParserInstanceHostKinds.Local, now);
        context.ParserInstanceConfigurations.AddRange(localInstance, secondInstance);
        context.ParserInstanceProxyAssignments.Add(new ParserInstanceProxyAssignment(localInstance.Id, proxy1.Id, now));
        context.ParserInstanceProxyAssignments.Add(new ParserInstanceProxyAssignment(secondInstance.Id, proxy2.Id, now));
        await context.SaveChangesAsync();

        var result = await service.GetRuntimeAssignmentsAsync("parser-local-01", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var runtimeProxy = Assert.Single(result.Value!.Proxies);
        Assert.Equal(proxy1.Id.ToString("D"), runtimeProxy.Key);
        Assert.Equal("http://10.0.0.1:8080", runtimeProxy.BaseUrl);
        Assert.Equal("secret1", runtimeProxy.Credentials!.Password);
        var niche = Assert.Single(result.Value.Niches);
        Assert.Equal(proxy1.Id.ToString("D"), niche.ProxyKey);
        Assert.Equal("мужские кеды и кроссовки", niche.ParserSearchText);
        Assert.NotEqual(niche.SearchQuery, niche.ParserSearchText);
        Assert.Equal("menu_token_trusted", niche.ScopeAcceptanceMode);
        Assert.Empty(niche.AllowedSubjectIds);
    }

    [Fact]
    public async Task RuntimeAssignments_returns_allowed_subject_set_when_active_mappings_exist()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);
        await service.CreateAsync(
            new CreateParserProxyRequest("10.0.0.1", 8080, 1080, "user", "secret1", 8194),
            CancellationToken.None);

        var proxy = await context.ParserProxies.SingleAsync();
        var now = DateTime.UtcNow;
        context.WbCategoryScopeSubjectMappings.Add(new WbCategoryScopeSubjectMapping(
            8194,
            "menu_redirect_subject_v2_8194",
            "Обувь / Мужская / Кеды и кроссовки",
            631,
            "Sneakers",
            WbCategoryScopeSubjectMappingStatuses.Active,
            WbCategoryScopeSubjectMappingSources.Manual,
            now));
        var localInstance = new ParserInstanceConfiguration("parser-local-01", "Local parser", ParserInstanceHostKinds.Local, now);
        context.ParserInstanceConfigurations.Add(localInstance);
        context.ParserInstanceProxyAssignments.Add(new ParserInstanceProxyAssignment(localInstance.Id, proxy.Id, now));
        await context.SaveChangesAsync();

        var result = await service.GetRuntimeAssignmentsAsync("parser-local-01", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var niche = Assert.Single(result.Value!.Niches);
        Assert.Equal("allowed_subject_set", niche.ScopeAcceptanceMode);
        Assert.Equal([631], niche.AllowedSubjectIds);
    }

    [Fact]
    public async Task RuntimeAssignments_returns_bad_request_when_proxy_password_cannot_be_decrypted()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var createService = CreateService(context, adminRole.Id);
        await createService.CreateAsync(
            new CreateParserProxyRequest("10.0.0.1", 8080, 1080, "user", "secret1", 8194),
            CancellationToken.None);

        var proxy = await context.ParserProxies.SingleAsync();
        var now = DateTime.UtcNow;
        var localInstance = new ParserInstanceConfiguration("parser-local-01", "Local parser", ParserInstanceHostKinds.Local, now);
        context.ParserInstanceConfigurations.Add(localInstance);
        context.ParserInstanceProxyAssignments.Add(new ParserInstanceProxyAssignment(localInstance.Id, proxy.Id, now));
        await context.SaveChangesAsync();
        var runtimeService = CreateService(context, adminRole.Id, new ThrowingSecretProtector());

        var result = await runtimeService.GetRuntimeAssignmentsAsync("parser-local-01", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.BadRequest, result.Error!.Type);
        Assert.Contains("unreadable password", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_does_not_reuse_deleted_proxy_key()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);
        var first = await service.CreateAsync(
            new CreateParserProxyRequest("10.0.0.1", 8080, 1080, "user", "secret", 8194),
            CancellationToken.None);
        Assert.True(first.IsSuccess);

        var deleteResult = await service.DeleteAsync(first.Value!.Id, CancellationToken.None);
        var second = await service.CreateAsync(
            new CreateParserProxyRequest("10.0.0.2", 8080, 1080, "user", "secret", 8194),
            CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.Key, second.Value!.Key);
        Assert.Equal(second.Value.Id.ToString("D"), second.Value.Key);
    }

    [Fact]
    public async Task RuntimeAssignments_fails_when_instance_is_missing()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);

        var result = await service.GetRuntimeAssignmentsAsync("parser-missing", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task RuntimeReviewSyncState_returns_known_reviews_and_unanswered_reviews()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);
        AddReviewSummary(context, "1001", marketplaceFeedbackCount: 3, fetchedReviewsCount: 2, coverageStatus: "incomplete");
        AddReviewRow(context, "1001", "review-1", Utc(2026, 7, 4, 10), "old-run");
        AddReviewRow(context, "1001", "review-2", Utc(2026, 7, 4, 11), "old-run");
        AddReviewReplyRow(context, "1001", "review-2", "reply-2", "newer-run");
        await context.SaveChangesAsync();

        var result = await service.GetRuntimeReviewSyncStateAsync("1001", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.MarketplaceFeedbackCount);
        Assert.Equal(2, result.Value.FetchedReviewsCount);
        Assert.Equal("incomplete", result.Value.CoverageStatus);
        Assert.Equal(Utc(2026, 7, 4, 11), result.Value.LatestReviewDateUtc);
        Assert.Equal(["review-1", "review-2"], result.Value.KnownReviewIds.Order(StringComparer.Ordinal));
        Assert.Equal(["review-1"], result.Value.UnansweredReviewIds);
    }

    [Fact]
    public async Task Admin_list_is_forbidden_for_non_admin()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context, "Manager");
        var service = CreateService(context, role.Id);

        var result = await service.GetAdminListAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task DeleteAsync_removes_proxy_and_assignments()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var service = CreateService(context, adminRole.Id);
        var created = await service.CreateAsync(
            new CreateParserProxyRequest("10.0.0.1", 8080, 1080, "user", "secret", 8194),
            CancellationToken.None);
        var now = DateTime.UtcNow;
        var instance = new ParserInstanceConfiguration("parser-local-01", "Local parser", ParserInstanceHostKinds.Local, now);
        context.ParserInstanceConfigurations.Add(instance);
        context.ParserInstanceProxyAssignments.Add(new ParserInstanceProxyAssignment(instance.Id, created.Value!.Id, now));
        await context.SaveChangesAsync();

        var deleteResult = await service.DeleteAsync(created.Value.Id, CancellationToken.None);
        var listResult = await service.GetAdminListAsync(CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        Assert.True(listResult.IsSuccess);
        Assert.Empty(listResult.Value!);
        Assert.Empty(await context.ParserProxies.ToListAsync());
        Assert.Empty(await context.ParserProxyNicheAssignments.ToListAsync());
        Assert.Empty(await context.ParserInstanceProxyAssignments.ToListAsync());
    }

    private static ParserProxyManagementService CreateService(
        ApplicationDbContext context,
        Guid roleId,
        IParserProxySecretProtector? secretProtector = null)
    {
        return new ParserProxyManagementService(
            context,
            new TestCurrentUser(roleId),
            new TestCategoryCatalogService(),
            secretProtector ?? new TestSecretProtector());
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
            .UseInMemoryDatabase($"parser-proxy-management-{Guid.NewGuid()}")
            .Options;

        return new ParserProxyManagementTestDbContext(options);
    }

    private static void AddReviewSummary(
        ApplicationDbContext context,
        string wbProductId,
        int? marketplaceFeedbackCount,
        int fetchedReviewsCount,
        string coverageStatus)
    {
        context.ParserCurrentProductReviewsSummaries.Add(new ParserCurrentProductReviewsSummary(
            wbProductId,
            $"root-{wbProductId}",
            "Женщинам",
            "Платья и сарафаны",
            reviewsCount: fetchedReviewsCount,
            averageRating: 4.5m,
            recentNegativeCount: 0,
            lastReviewDateUtc: Utc(2026, 7, 4, 11),
            marketplaceFeedbackCount: marketplaceFeedbackCount,
            fetchedReviewsCount: fetchedReviewsCount,
            oldestReviewDateUtc: Utc(2026, 7, 4, 10),
            coverageStatus: coverageStatus,
            coverageSource: "product_full",
            lastCoverageError: null,
            reviewsHash: $"reviews-{wbProductId}",
            reviewsJson: "{}",
            observedAtUtc: Utc(2026, 7, 4, 12),
            batchId: $"batch-{wbProductId}"));
    }

    private static void AddReviewRow(
        ApplicationDbContext context,
        string wbProductId,
        string reviewId,
        DateTime createdAtOnMp,
        string parserRunId)
    {
        var line = context.ParserReviewRows.Count() + 1;
        context.ParserReviewRows.Add(new ParserReviewRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            line,
            $"review-row-{wbProductId}-{reviewId}",
            1,
            parserRunId,
            Utc(2026, 7, 4, 13).AddMinutes(line),
            "wildberries",
            null,
            null,
            $"root-{wbProductId}",
            wbProductId,
            "product_full",
            reviewId,
            5,
            null,
            null,
            null,
            createdAtOnMp,
            null,
            null,
            null,
            null,
            null,
            "Женщинам",
            "Платья и сарафаны",
            "Платья и сарафаны",
            "12354108",
            null,
            false,
            false,
            false));
    }

    private static void AddReviewReplyRow(
        ApplicationDbContext context,
        string wbProductId,
        string reviewId,
        string replyId,
        string parserRunId)
    {
        var line = context.ParserReviewReplyRows.Count() + 1;
        context.ParserReviewReplyRows.Add(new ParserReviewReplyRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            line,
            $"reply-row-{wbProductId}-{reviewId}-{replyId}",
            1,
            parserRunId,
            Utc(2026, 7, 4, 14).AddMinutes(line),
            "wildberries",
            null,
            null,
            $"root-{wbProductId}",
            wbProductId,
            "product_full",
            reviewId,
            replyId,
            null,
            "Спасибо за отзыв",
            Utc(2026, 7, 4, 14),
            null,
            "seller",
            null,
            "Женщинам",
            "Платья и сарафаны",
            "Платья и сарафаны",
            "12354108",
            null,
            false,
            false,
            false));
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

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

    private sealed class TestSecretProtector : IParserProxySecretProtector
    {
        public string Protect(string value) => $"protected::{value}";

        public string Unprotect(string protectedValue) => protectedValue.Replace("protected::", string.Empty, StringComparison.Ordinal);
    }

    private sealed class ThrowingSecretProtector : IParserProxySecretProtector
    {
        public string Protect(string value) => $"protected::{value}";

        public string Unprotect(string protectedValue) =>
            throw new CryptographicException("The key was not found in the key ring.");
    }

    private sealed class TestCategoryCatalogService : IWildberriesCategoryCatalogService
    {
        private static readonly IReadOnlyList<WildberriesCategoryNodeDto> Leaves =
        [
            new(
                8194,
                "Кеды и кроссовки",
                "Обувь",
                "Кеды и кроссовки",
                "Обувь / Мужская / Кеды и кроссовки",
                "menu_redirect_subject_v2_8194 мужские кеды и кроссовки",
                "human search",
                100,
                true,
                2)
        ];

        public Task<ServiceResult<WildberriesCategoryCatalogDto>> GetTreeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ServiceResult<WildberriesCategoryCatalogDto>.Success(
                new WildberriesCategoryCatalogDto("wildberries", DateTime.UtcNow, Leaves)));
        }

        public Task<ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>> GetLeavesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.Success(Leaves));
        }

        public Task<ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>> SearchAsync(
            WildberriesCategorySearchQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.Success(Leaves));
        }
    }

    private sealed class ParserProxyManagementTestDbContext : ApplicationDbContext
    {
        public ParserProxyManagementTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
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
                typeof(ParserReviewRow),
                typeof(ParserReviewReplyRow),
                typeof(ParserCurrentProductReviewsSummary),
                typeof(WbCategoryScopeSubjectMapping)
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
            modelBuilder.Entity<ParserReviewRow>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserReviewReplyRow>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserCurrentProductReviewsSummary>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<WbCategoryScopeSubjectMapping>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
