using System.Text.Json;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserBatches;

public sealed class ParserRunRollbackServiceTests
{
    [Fact]
    public async Task PreviewAsync_returns_noop_message_when_run_downloaded_no_products()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var run = CompletedRun(downloadedProductsCount: 0);
        context.ParserProxyRuns.Add(run);
        await context.SaveChangesAsync();
        var service = new ParserRunRollbackService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.PreviewAsync(run.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.CanRollback);
        Assert.Equal("Новых данных получено не было, откатывать нечего", result.Value.Message);
    }

    [Fact]
    public async Task RollbackAsync_deletes_current_rows_created_by_run()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var run = CompletedRun(downloadedProductsCount: 100);
        var batch = CompletedBatch(run);
        var current = CurrentProduct("1001", priceDiscounted: 120);
        var logistics = new ParserCurrentProductLogistics(
            "1001",
            "root-1001",
            "category",
            "niche",
            "hash-logistics",
            "{}",
            Utc(2026, 7, 3, 10),
            batch.ExternalBatchId);
        context.ParserProxyRuns.Add(run);
        context.ParserBatchSubmissions.Add(batch);
        context.ParserCurrentProductRows.Add(current);
        context.ParserCurrentProductLogistics.Add(logistics);
        context.ParserRunProductEffects.Add(new ParserRunProductEffect(
            run.Id,
            batch.Id,
            current.WbProductId,
            current.Id,
            ParserRunProductEffectTypes.Created,
            Utc(2026, 7, 3, 10)));
        context.ParserRunCurrentEntityEffects.Add(new ParserRunCurrentEntityEffect(
            run.Id,
            batch.Id,
            current.WbProductId,
            ParserRunCurrentEntityKinds.Product,
            current.Id.ToString("D"),
            ParserRunCurrentEntityEffectTypes.Created,
            null,
            JsonSerializer.Serialize(ParserRunCurrentEntitySnapshots.FromProduct(current)),
            Utc(2026, 7, 3, 10)));
        context.ParserRunCurrentEntityEffects.Add(new ParserRunCurrentEntityEffect(
            run.Id,
            batch.Id,
            logistics.WbProductId,
            ParserRunCurrentEntityKinds.Logistics,
            logistics.Id.ToString("D"),
            ParserRunCurrentEntityEffectTypes.Created,
            null,
            JsonSerializer.Serialize(ParserRunCurrentEntitySnapshots.FromLogistics(logistics)),
            Utc(2026, 7, 3, 10)));
        await context.SaveChangesAsync();
        var service = new ParserRunRollbackService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RollbackAsync(run.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.DeletedProductsCount);
        Assert.Equal(0, await context.ParserCurrentProductRows.CountAsync());
        Assert.Equal(0, await context.ParserCurrentProductLogistics.CountAsync());
        Assert.Equal(ParserRunRollbackStatuses.Completed, await context.ParserRunRollbacks.Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task RollbackAsync_restores_current_product_groups_updated_by_run()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var run = CompletedRun(downloadedProductsCount: 100);
        var batch = CompletedBatch(run);
        var before = CurrentProduct("1001", priceDiscounted: 100);
        var after = CurrentProduct("1001", priceDiscounted: 120);
        context.ParserProxyRuns.Add(run);
        context.ParserBatchSubmissions.Add(batch);
        context.ParserCurrentProductRows.Add(after);
        context.ParserRunProductEffects.Add(new ParserRunProductEffect(
            run.Id,
            batch.Id,
            after.WbProductId,
            after.Id,
            ParserRunProductEffectTypes.Updated,
            Utc(2026, 7, 3, 10)));
        context.ParserRunCurrentEntityEffects.Add(new ParserRunCurrentEntityEffect(
            run.Id,
            batch.Id,
            after.WbProductId,
            ParserRunCurrentEntityKinds.Product,
            after.Id.ToString("D"),
            ParserRunCurrentEntityEffectTypes.Updated,
            JsonSerializer.Serialize(ParserRunCurrentEntitySnapshots.FromProduct(before)),
            JsonSerializer.Serialize(ParserRunCurrentEntitySnapshots.FromProduct(after)),
            Utc(2026, 7, 3, 10)));
        await context.SaveChangesAsync();
        var service = new ParserRunRollbackService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RollbackAsync(run.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var restored = await context.ParserCurrentProductRows.SingleAsync();
        Assert.Equal(100, restored.PriceDiscounted);
        Assert.Equal(1, result.Value!.RestoredProductsCount);
        Assert.Contains(
            await context.ParserProductChangeEvents.Select(x => x.ChangeType).ToListAsync(),
            x => x == "rollback");
    }

    [Fact]
    public async Task RollbackAsync_blocks_when_newer_effect_touched_same_product()
    {
        await using var context = CreateContext();
        var adminRole = await SeedRoleAsync(context, "Admin");
        var run = CompletedRun(downloadedProductsCount: 100, externalId: "run-old", startedAtUtc: Utc(2026, 7, 3, 10));
        var newerRun = CompletedRun(downloadedProductsCount: 100, externalId: "run-new", startedAtUtc: Utc(2026, 7, 3, 11));
        var batch = CompletedBatch(run);
        var newerBatch = CompletedBatch(newerRun, "batch-new");
        var current = CurrentProduct("1001", priceDiscounted: 120);
        context.ParserProxyRuns.AddRange(run, newerRun);
        context.ParserBatchSubmissions.AddRange(batch, newerBatch);
        context.ParserCurrentProductRows.Add(current);
        context.ParserRunProductEffects.Add(new ParserRunProductEffect(
            run.Id,
            batch.Id,
            current.WbProductId,
            current.Id,
            ParserRunProductEffectTypes.Updated,
            Utc(2026, 7, 3, 10)));
        context.ParserRunProductEffects.Add(new ParserRunProductEffect(
            newerRun.Id,
            newerBatch.Id,
            current.WbProductId,
            current.Id,
            ParserRunProductEffectTypes.Updated,
            Utc(2026, 7, 3, 11)));
        await context.SaveChangesAsync();
        var service = new ParserRunRollbackService(context, new TestCurrentUser(adminRole.Id));

        var result = await service.RollbackAsync(run.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Equal(120, await context.ParserCurrentProductRows.Select(x => x.PriceDiscounted).SingleAsync());
    }

    private static ParserProxyRun CompletedRun(
        int downloadedProductsCount,
        string externalId = "run-1",
        DateTime? startedAtUtc = null)
    {
        var started = startedAtUtc ?? Utc(2026, 7, 3, 10);
        var run = new ParserProxyRun(
            "parser-1",
            externalId,
            "cycle-1",
            ParserProxyRunCycleKinds.Diagnostic,
            "proxy-1",
            "category",
            "niche",
            downloadedProductsCount,
            downloadedProductsCount,
            "203.0.113.10",
            null,
            "valid",
            started);
        run.Complete(downloadedProductsCount, downloadedProductsCount, started.AddMinutes(1));
        return run;
    }

    private static ParserBatchSubmission CompletedBatch(ParserProxyRun run, string externalBatchId = "cycle-1:batch:0001")
    {
        var batch = new ParserBatchSubmission(
            run.ParserInstanceId,
            externalBatchId,
            run.SourceCategory,
            run.SourceSubcategory,
            run.ProxyKey,
            "complete_card_batch",
            "sha256:test",
            run.StartedAtUtc);
        batch.AssignProxyRun(run.Id);
        batch.MarkCompleted(run.StartedAtUtc.AddMinutes(1));
        return batch;
    }

    private static ParserCurrentProductRow CurrentProduct(string wbProductId, decimal priceDiscounted)
    {
        var row = new ParserProductRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            $"hash-{wbProductId}-{priceDiscounted}",
            1,
            "wildberries",
            "products-run",
            Utc(2026, 7, 3, 10),
            "category",
            "niche",
            null,
            "12354108",
            wbProductId,
            null,
            "Product",
            null,
            10,
            "Brand",
            20,
            "Seller",
            priceDiscounted + 10,
            priceDiscounted,
            priceDiscounted - 5,
            10,
            5,
            5,
            4.9m,
            11,
            "wb",
            null,
            1,
            $"root-{wbProductId}",
            1,
            2,
            null);
        return new ParserCurrentProductRow(
            row,
            new ProductGroupHashes(
                "hash-identity",
                $"hash-price-{priceDiscounted}",
                "hash-stock",
                "hash-rating",
                "hash-reviews",
                "hash-media",
                "hash-seller"),
            Utc(2026, 7, 3, 10));
    }

    private static async Task<Role> SeedRoleAsync(ApplicationDbContext context, string name)
    {
        var now = DateTime.UtcNow;
        var role = new Role(name, null, now, now);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static DateTime Utc(int year, int month, int day, int hour) =>
        DateTime.SpecifyKind(new DateTime(year, month, day, hour, 0, 0), DateTimeKind.Utc);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-run-rollback-{Guid.NewGuid()}")
            .Options;
        return new ParserRunRollbackTestDbContext(options);
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

    private sealed class ParserRunRollbackTestDbContext : ApplicationDbContext
    {
        public ParserRunRollbackTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var keptTypes = new HashSet<Type>
            {
                typeof(Role),
                typeof(ParserProxyRun),
                typeof(ParserBatchSubmission),
                typeof(ParserProductRow),
                typeof(ParserCurrentProductRow),
                typeof(ParserCurrentProductDetail),
                typeof(ParserCurrentProductLogistics),
                typeof(ParserCurrentProductRank),
                typeof(ParserCurrentProductReviewsSummary),
                typeof(ParserCurrentProductReviewEvidence),
                typeof(ParserProductChangeEvent),
                typeof(ParserRunProductEffect),
                typeof(ParserRunCurrentEntityEffect),
                typeof(ParserRunRollback)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (!keptTypes.Contains(entityType))
                    modelBuilder.Ignore(entityType);
            }

            modelBuilder.Entity<Role>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserProxyRun>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserBatchSubmission>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserProductRow>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductRow>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductDetail>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductDetail>().Ignore(x => x.DetailsJson);
            modelBuilder.Entity<ParserCurrentProductLogistics>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductLogistics>().Ignore(x => x.LogisticsJson);
            modelBuilder.Entity<ParserCurrentProductRank>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductRank>().Ignore(x => x.RankJson);
            modelBuilder.Entity<ParserCurrentProductReviewsSummary>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductReviewsSummary>().Ignore(x => x.ReviewsJson);
            modelBuilder.Entity<ParserCurrentProductReviewEvidence>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductReviewEvidence>().Ignore(x => x.ReviewJson);
            modelBuilder.Entity<ParserProductChangeEvent>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserProductChangeEvent>().Ignore(x => x.OldValueJson);
            modelBuilder.Entity<ParserProductChangeEvent>().Ignore(x => x.NewValueJson);
            modelBuilder.Entity<ParserRunProductEffect>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserRunCurrentEntityEffect>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserRunRollback>().HasKey(x => x.Id);
        }
    }
}
