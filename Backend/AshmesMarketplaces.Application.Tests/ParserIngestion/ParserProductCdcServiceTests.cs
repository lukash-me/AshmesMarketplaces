using AshmesMarketplaces.Application.ParserIngestion.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserIngestion;

public sealed class ParserProductCdcServiceTests
{
    [Fact]
    public async Task NewProductCreatesCurrentRowAndCreatedEvent()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);
        var row = ProductRow("1001", priceDiscounted: 100, quantity: 10);

        await service.ApplyProductRowsAsync([row], "batch-1", CancellationToken.None);

        var current = await context.ParserCurrentProductRows.SingleAsync();
        var change = await context.ParserProductChangeEvents.SingleAsync();
        Assert.Equal("1001", current.WbProductId);
        Assert.Equal("created", change.ChangeType);
        Assert.Equal("identity", change.FieldGroup);
        Assert.Null(change.OldHash);
        Assert.NotNull(change.NewHash);
    }

    [Fact]
    public async Task SameProductStateDoesNotCreateSecondChangeEvent()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);

        await service.ApplyProductRowsAsync([ProductRow("1001", priceDiscounted: 100, quantity: 10)], "batch-1", CancellationToken.None);
        await service.ApplyProductRowsAsync([ProductRow("1001", priceDiscounted: 100, quantity: 10)], "batch-1", CancellationToken.None);

        Assert.Equal(1, await context.ParserProductChangeEvents.CountAsync());
    }

    [Fact]
    public async Task SameProductStateUpdatesLastSeenWithoutRunEffect()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);
        var runId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        await service.ApplyProductRowsAsync(
            [ProductRow("1001", priceDiscounted: 100, quantity: 10)],
            "batch-1",
            new ParserCdcApplyContext(runId, batchId),
            CancellationToken.None);

        var current = await context.ParserCurrentProductRows.SingleAsync();
        current.MarkSeen(
            "old-cycle",
            Guid.NewGuid(),
            DateTime.SpecifyKind(new DateTime(2026, 7, 1, 10, 0, 0), DateTimeKind.Utc));
        await context.SaveChangesAsync();

        await service.ApplyProductRowsAsync(
            [ProductRow("1001", priceDiscounted: 100, quantity: 10)],
            "batch-2",
            new ParserCdcApplyContext(runId, batchId),
            CancellationToken.None);

        var stored = await context.ParserCurrentProductRows.SingleAsync();
        Assert.Equal("1001", stored.WbProductId);
        Assert.Equal(runId.ToString("D"), stored.LastSeenParserProxyRunId);
        Assert.Equal(ParserMarketplacePresenceStatuses.Active, stored.MarketplacePresenceStatus);
        Assert.Equal(1, await context.ParserRunProductEffects.CountAsync());
    }

    [Fact]
    public async Task MissingProductSeenAgainBecomesActiveAndWritesPresenceEvent()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);

        await service.ApplyProductRowsAsync(
            [ProductRow("1001", priceDiscounted: 100, quantity: 10)],
            "batch-1",
            CancellationToken.None);

        var current = await context.ParserCurrentProductRows.SingleAsync();
        current.MarkMissingInFullScan("cycle-old", DateTime.SpecifyKind(new DateTime(2026, 7, 1, 10, 0, 0), DateTimeKind.Utc));
        await context.SaveChangesAsync();

        var runId = Guid.NewGuid();
        await service.ApplyProductRowsAsync(
            [ProductRow("1001", priceDiscounted: 100, quantity: 10)],
            "batch-2",
            new ParserCdcApplyContext(runId, Guid.NewGuid()),
            CancellationToken.None);

        var stored = await context.ParserCurrentProductRows.SingleAsync();
        var presenceEvent = await context.ParserProductPresenceEvents.SingleAsync();
        Assert.Equal(ParserMarketplacePresenceStatuses.Active, stored.MarketplacePresenceStatus);
        Assert.Equal(ParserMarketplacePresenceStatuses.MissingInLatestFullScan, presenceEvent.OldStatus);
        Assert.Equal(ParserMarketplacePresenceStatuses.Active, presenceEvent.NewStatus);
        Assert.Equal("seen_after_missing", presenceEvent.Reason);
    }

    [Fact]
    public async Task PriceChangeCreatesOnlyPriceEvent()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);

        await service.ApplyProductRowsAsync([ProductRow("1001", priceDiscounted: 100, quantity: 10)], "batch-1", CancellationToken.None);
        await service.ApplyProductRowsAsync([ProductRow("1001", priceDiscounted: 120, quantity: 10)], "batch-2", CancellationToken.None);

        var updates = await context.ParserProductChangeEvents
            .Where(x => x.ChangeType == "updated")
            .Select(x => x.FieldGroup)
            .ToListAsync();
        Assert.Equal(["price"], updates);
    }

    [Fact]
    public async Task ApplyProductRowsAsync_records_run_effects_when_context_is_provided()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);
        var runId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var contextInfo = new ParserCdcApplyContext(runId, batchId);

        await service.ApplyProductRowsAsync(
            [ProductRow("1001", priceDiscounted: 100, quantity: 10)],
            "batch-1",
            contextInfo,
            CancellationToken.None);
        await service.ApplyProductRowsAsync(
            [ProductRow("1001", priceDiscounted: 120, quantity: 10)],
            "batch-2",
            contextInfo,
            CancellationToken.None);

        var productEffects = await context.ParserRunProductEffects
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.EffectType)
            .ToListAsync();
        var entityEffects = await context.ParserRunCurrentEntityEffects
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new { x.EntityKind, x.EffectType, x.OldValueJson, x.NewValueJson })
            .ToListAsync();

        Assert.Equal([ParserRunProductEffectTypes.Created, ParserRunProductEffectTypes.Updated], productEffects);
        Assert.Contains(entityEffects, x =>
            x.EntityKind == ParserRunCurrentEntityKinds.Product &&
            x.EffectType == ParserRunCurrentEntityEffectTypes.Created &&
            x.OldValueJson == null &&
            x.NewValueJson != null);
        Assert.Contains(entityEffects, x =>
            x.EntityKind == ParserRunCurrentEntityKinds.Product &&
            x.EffectType == ParserRunCurrentEntityEffectTypes.Updated &&
            x.OldValueJson != null &&
            x.NewValueJson != null);
    }

    [Fact]
    public async Task ExistingProductChangeUsesServerReceiptDateForChangeEvent()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);

        await service.ApplyProductRowsAsync(
            [ProductRow("1001", priceDiscounted: 100, quantity: 10)],
            "batch-1",
            CancellationToken.None);

        var parserDate = DateTime.SpecifyKind(new DateTime(2024, 1, 10, 9, 0, 0), DateTimeKind.Utc);
        var beforeServerReceipt = DateTime.UtcNow.AddSeconds(-1);

        await service.ApplyProductRowsAsync(
            [ProductRow("1001", priceDiscounted: 120, quantity: 10, parsedAtUtc: parserDate)],
            "batch-2",
            CancellationToken.None);

        var afterServerReceipt = DateTime.UtcNow.AddSeconds(1);
        var change = await context.ParserProductChangeEvents.SingleAsync(x => x.ChangeType == "updated" && x.FieldGroup == "price");

        Assert.NotEqual(parserDate, change.ObservedAtUtc);
        Assert.InRange(change.ObservedAtUtc, beforeServerReceipt, afterServerReceipt);
    }

    [Fact]
    public async Task LogisticsChangeCreatesOnlyLogisticsEvent()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);

        await service.ApplyLogisticsRowsAsync([LogisticsRow("1001", quantity: 10, deliveryLabel: "Завтра")], "batch-1", CancellationToken.None);
        await service.ApplyLogisticsRowsAsync([LogisticsRow("1001", quantity: 10, deliveryLabel: "Через 3 дня")], "batch-2", CancellationToken.None);

        var events = await context.ParserProductChangeEvents
            .Select(x => new { x.ChangeType, x.FieldGroup })
            .ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Contains(events, x => x.ChangeType == "created" && x.FieldGroup == "logistics");
        Assert.Contains(events, x => x.ChangeType == "updated" && x.FieldGroup == "logistics");
    }

    [Fact]
    public async Task ChangedReviewCreatesReviewLevelEvent()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);

        await service.ApplyReviewRowsAsync([ReviewRow("1001", "review-1", "first text", rating: 5)], "batch-1", CancellationToken.None);
        await service.ApplyReviewRowsAsync([ReviewRow("1001", "review-1", "edited text", rating: 5)], "batch-2", CancellationToken.None);

        var events = await context.ParserProductChangeEvents
            .Where(x => x.FieldGroup == "reviews")
            .Select(x => x.ChangeType)
            .ToListAsync();
        Assert.Equal(["review_created", "review_updated"], events);
        Assert.Equal(1, await context.ParserCurrentProductReviewEvidence.CountAsync());
    }

    [Fact]
    public async Task ReviewCoverageWithoutReviewRowsCreatesCurrentSummary()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);

        await service.ApplyProductRowsAsync(
            [ProductRow("175139943", priceDiscounted: 556, quantity: 1)],
            "batch-products",
            CancellationToken.None);

        await service.ApplyReviewCoverageRowsAsync(
            [
                new ParserReviewCoverageSnapshot(
                    WbProductId: "175139943",
                    WbRootId: "174524123",
                    MarketplaceFeedbackCount: 0,
                    FetchedReviewsCount: 0,
                    CoverageStatus: "full",
                    CoverageSource: "root_variant_filtered",
                    LastCoverageError: null,
                    ObservedAtUtc: DateTime.SpecifyKind(new DateTime(2026, 7, 5, 11, 1, 9), DateTimeKind.Utc))
            ],
            "batch-coverage",
            CancellationToken.None);

        var summary = await context.ParserCurrentProductReviewsSummaries.SingleAsync();
        Assert.Equal("175139943", summary.WbProductId);
        Assert.Equal("174524123", summary.WbRootId);
        Assert.Equal(0, summary.ReviewsCount);
        Assert.Equal(0, summary.MarketplaceFeedbackCount);
        Assert.Equal(0, summary.FetchedReviewsCount);
        Assert.Equal("full", summary.CoverageStatus);
        Assert.Equal("root_variant_filtered", summary.CoverageSource);
    }

    [Fact]
    public async Task ExistingProductChangesFromDifferentTablesCreateEventsForEachChangedGroup()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);

        await service.ApplyLogisticsRowsAsync(
            [LogisticsRow("1001", quantity: 10, deliveryLabel: "Завтра")],
            "batch-1",
            CancellationToken.None);
        await service.ApplyReviewRowsAsync(
            [ReviewRow("1001", "review-1", "first text", rating: 5)],
            "batch-1",
            CancellationToken.None);

        await service.ApplyLogisticsRowsAsync(
            [LogisticsRow("1001", quantity: 7, deliveryLabel: "Через 3 дня")],
            "batch-2",
            CancellationToken.None);
        await service.ApplyReviewRowsAsync(
            [ReviewRow("1001", "review-1", "edited text", rating: 3)],
            "batch-2",
            CancellationToken.None);

        var updates = await context.ParserProductChangeEvents
            .Where(x => x.BatchId == "batch-2")
            .OrderBy(x => x.FieldGroup)
            .Select(x => new { x.FieldGroup, x.ChangeType })
            .ToListAsync();

        Assert.Equal(2, updates.Count);
        Assert.Contains(updates, x => x.FieldGroup == "logistics" && x.ChangeType == "updated");
        Assert.Contains(updates, x => x.FieldGroup == "reviews" && x.ChangeType == "review_updated");
    }

    [Fact]
    public async Task RankRowsUpdateExistingCurrentProductPosition()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);

        await service.ApplyProductRowsAsync(
            [ProductRow("1001", priceDiscounted: 100, quantity: 10)],
            "batch-products",
            CancellationToken.None);

        await service.ApplyRankRowsAsync(
            [RankRow("1001", absolutePosition: 7)],
            "batch-ranks",
            CancellationToken.None);

        var current = await context.ParserCurrentProductRows.SingleAsync();
        Assert.Equal("observed", current.PositionState);
        Assert.Equal(7, current.PositionAbsolute);
        Assert.Equal("Платья и сарафаны", current.PositionQuery);
    }

    [Fact]
    public async Task ProductRowsAppliedAfterRanksStillReceivePosition()
    {
        await using var context = CreateContext();
        var service = new ParserProductCdcService(context);

        await service.ApplyRankRowsAsync(
            [RankRow("1001", absolutePosition: 11)],
            "batch-ranks",
            CancellationToken.None);

        await service.ApplyProductRowsAsync(
            [ProductRow("1001", priceDiscounted: 100, quantity: 10)],
            "batch-products",
            CancellationToken.None);

        var current = await context.ParserCurrentProductRows.SingleAsync();
        Assert.Equal("observed", current.PositionState);
        Assert.Equal(11, current.PositionAbsolute);
        Assert.Equal("Платья и сарафаны", current.PositionQuery);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-cdc-{Guid.NewGuid()}")
            .Options;
        return new ParserCdcTestDbContext(options);
    }

    private static ParserProductRow ProductRow(
        string wbProductId,
        decimal priceDiscounted,
        int quantity,
        DateTime? parsedAtUtc = null)
    {
        return new ParserProductRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            $"hash-{wbProductId}-{priceDiscounted}-{quantity}",
            1,
            "wildberries",
            "products-run",
            parsedAtUtc ?? DateTime.SpecifyKind(new DateTime(2026, 6, 29, 10, 0, 0), DateTimeKind.Utc),
            "Товары для дома",
            "Коврики для ванной",
            null,
            "12354108",
            wbProductId,
            null,
            "Коврик",
            null,
            10,
            "Brand",
            20,
            "Seller",
            priceDiscounted + 50,
            priceDiscounted,
            priceDiscounted - 10,
            15,
            quantity,
            5,
            4.9m,
            12,
            "wb",
            null,
            3,
            $"root-{wbProductId}",
            1,
            2,
            null);
    }

    private static ParserRankSnapshotRow RankRow(string wbProductId, int absolutePosition)
    {
        return new ParserRankSnapshotRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            absolutePosition,
            $"rank-{wbProductId}-{absolutePosition}",
            1,
            "ranks-run",
            DateTime.SpecifyKind(new DateTime(2026, 6, 29, 10, 0, 0), DateTimeKind.Utc),
            "wildberries",
            "proxy-1-Платья",
            "search_query",
            "Товары для дома",
            "Коврики для ванной",
            "Платья и сарафаны",
            "12354108",
            "popular",
            null,
            "request",
            1,
            absolutePosition,
            absolutePosition,
            wbProductId,
            $"root-{wbProductId}",
            1000,
            "ok");
    }

    private static ParserLogisticsSnapshotRow LogisticsRow(
        string wbProductId,
        int quantity,
        string deliveryLabel)
    {
        return new ParserLogisticsSnapshotRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            $"logistics-{wbProductId}-{quantity}-{deliveryLabel}",
            1,
            "logistics-run",
            "wildberries",
            DateTime.SpecifyKind(new DateTime(2026, 6, 29, 10, 0, 0), DateTimeKind.Utc),
            "card",
            "wb-logistics",
            $"request-{wbProductId}",
            "12354108",
            "central",
            "Москва",
            "v1",
            "Москва",
            "Москва",
            null,
            null,
            null,
            "Товары для дома",
            "Коврики для ванной",
            null,
            wbProductId,
            $"root-{wbProductId}",
            "20",
            "Seller",
            quantity,
            false,
            null,
            "exact",
            null,
            1,
            2,
            null,
            null,
            "ok",
            deliveryLabel,
            DateTime.SpecifyKind(new DateTime(2026, 6, 30), DateTimeKind.Utc),
            "wb",
            DateTime.SpecifyKind(new DateTime(2026, 6, 29, 10, 0, 0), DateTimeKind.Utc),
            null,
            null);
    }

    private static ParserReviewRow ReviewRow(string wbProductId, string reviewId, string text, int rating)
    {
        return new ParserReviewRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            1,
            $"review-{wbProductId}-{reviewId}-{text}",
            1,
            "reviews-run",
            DateTime.SpecifyKind(new DateTime(2026, 6, 29, 10, 0, 0), DateTimeKind.Utc),
            "wildberries",
            null,
            null,
            $"root-{wbProductId}",
            wbProductId,
            "product",
            reviewId,
            rating,
            text,
            "pros",
            "cons",
            DateTime.SpecifyKind(new DateTime(2026, 6, 28, 10, 0, 0), DateTimeKind.Utc),
            "buyer",
            null,
            false,
            0,
            0,
            "category",
            "subcategory",
            null,
            "12354108",
            null,
            false,
            false,
            false);
    }

    private sealed class ParserCdcTestDbContext : ApplicationDbContext
    {
        public ParserCdcTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var keptTypes = new HashSet<Type>
            {
                typeof(ParserCurrentProductRow),
                typeof(ParserCurrentProductLogistics),
                typeof(ParserCurrentProductDetail),
                typeof(ParserCurrentProductRank),
                typeof(ParserCurrentProductReviewEvidence),
                typeof(ParserCurrentProductReviewsSummary),
                typeof(ParserRankSnapshotRow),
                typeof(ParserProductChangeEvent),
                typeof(ParserProductPresenceEvent),
                typeof(ParserRunProductEffect),
                typeof(ParserRunCurrentEntityEffect)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
            {
                if (!keptTypes.Contains(entityType.ClrType))
                    modelBuilder.Ignore(entityType.ClrType);
            }

            modelBuilder.Entity<ParserCurrentProductRow>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductLogistics>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductLogistics>().Ignore(x => x.LogisticsJson);
            modelBuilder.Entity<ParserCurrentProductDetail>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductDetail>().Ignore(x => x.DetailsJson);
            modelBuilder.Entity<ParserCurrentProductRank>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductRank>().Ignore(x => x.RankJson);
            modelBuilder.Entity<ParserCurrentProductReviewEvidence>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductReviewEvidence>().Ignore(x => x.ReviewJson);
            modelBuilder.Entity<ParserCurrentProductReviewsSummary>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserCurrentProductReviewsSummary>().Ignore(x => x.ReviewsJson);
            modelBuilder.Entity<ParserRankSnapshotRow>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserRankSnapshotRow>().Ignore(x => x.Filters);
            modelBuilder.Entity<ParserProductChangeEvent>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserProductChangeEvent>().Ignore(x => x.OldValueJson);
            modelBuilder.Entity<ParserProductChangeEvent>().Ignore(x => x.NewValueJson);
            modelBuilder.Entity<ParserProductPresenceEvent>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserRunProductEffect>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserRunCurrentEntityEffect>().HasKey(x => x.Id);
        }
    }
}
