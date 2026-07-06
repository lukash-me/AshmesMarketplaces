using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using AshmesMarketplaces.Application.MarketIntelligence.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.MarketIntelligence;

public sealed class PublicMarketIntelligenceRefreshServiceTests
{
    [Fact]
    public async Task RefreshAllAsync_uses_only_contexts_with_rank_observations()
    {
        await using var context = CreateContext();
        SeedRankObservation(context, "Женщинам", "Платья и сарафаны", "Платья и сарафаны");
        await context.SaveChangesAsync();
        var readService = new FakeMarketIntelligenceReadService();
        var service = new PublicMarketIntelligenceRefreshService(context, readService);

        var result = await service.RefreshAllAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Single(readService.Requests);
        Assert.Equal("Платья и сарафаны", readService.Requests[0].SourceSubcategory);
        Assert.Equal("Платья и сарафаны", readService.Requests[0].Query);
        var snapshot = await context.PublicMarketIntelligenceSnapshots.SingleAsync();
        Assert.Equal("Платья и сарафаны", snapshot.Query);
        Assert.Equal(PublicMarketIntelligenceSnapshot.CompletedStatus, snapshot.Status);
    }

    [Fact]
    public async Task SnapshotReadService_falls_back_to_latest_snapshot_for_same_niche_when_query_changed()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        var snapshot = new PublicMarketIntelligenceSnapshot(
            "Женщинам",
            "Платья и сарафаны",
            "Платья и сарафаны",
            PublicMarketIntelligenceContextCatalog.SourceRegionDest,
            PublicMarketIntelligenceContextCatalog.Sort,
            PublicMarketIntelligenceContextCatalog.TopN,
            now);
        snapshot.MarkCompleted(
            System.Text.Json.JsonSerializer.Serialize(BuildDto(
                "Женщинам",
                "Платья и сарафаны",
                "Платья и сарафаны")),
            1,
            now,
            now,
            10);
        context.PublicMarketIntelligenceSnapshots.Add(snapshot);
        await context.SaveChangesAsync();
        var service = new PublicMarketIntelligenceSnapshotReadService(context);

        var result = await service.GetAsync(
            new PublicMarketIntelligenceQuery
            {
                SourceCategory = "Женщинам",
                SourceSubcategory = "Платья и сарафаны",
                Query = "menu_v3_8137 платье женские",
                SourceRegionDest = PublicMarketIntelligenceContextCatalog.SourceRegionDest,
                Sort = PublicMarketIntelligenceContextCatalog.Sort,
                TopN = PublicMarketIntelligenceContextCatalog.TopN
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Платья и сарафаны", result.Value!.Context.Query);
    }

    [Fact]
    public async Task ConcentrationRefreshAllAsync_uses_only_contexts_with_rank_observations()
    {
        await using var context = CreateContext();
        SeedRankObservation(context, "Женщинам", "Платья и сарафаны", "Платья и сарафаны");
        await context.SaveChangesAsync();
        var readService = new FakeMarketIntelligenceReadService();
        var service = new PublicMarketConcentrationRefreshService(context, readService);

        var result = await service.RefreshAllAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Single(readService.Requests);
        Assert.Equal("Платья и сарафаны", readService.Requests[0].SourceSubcategory);
        Assert.Equal("Платья и сарафаны", readService.Requests[0].Query);
        var snapshot = await context.PublicMarketConcentrationSnapshots.SingleAsync();
        Assert.Equal("Платья и сарафаны", snapshot.Query);
        Assert.Equal(PublicMarketConcentrationSnapshot.CompletedStatus, snapshot.Status);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"market-intelligence-refresh-{Guid.NewGuid()}")
            .Options;

        return new MarketIntelligenceRefreshTestDbContext(options);
    }

    private static void SeedRankObservation(
        ApplicationDbContext context,
        string sourceCategory,
        string sourceSubcategory,
        string query)
    {
        context.ParserRankSnapshotRows.Add(new ParserRankSnapshotRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid().ToString("N"),
            1,
            "rank-run-1",
            DateTime.UtcNow,
            "wildberries",
            "ctx-1",
            "search",
            sourceCategory,
            sourceSubcategory,
            query,
            PublicMarketIntelligenceContextCatalog.SourceRegionDest,
            PublicMarketIntelligenceContextCatalog.Sort,
            null,
            "fingerprint",
            1,
            1,
            1,
            "123",
            "root-1",
            1,
            "succeeded"));
    }

    private static PublicMarketIntelligenceDto BuildDto(
        string sourceCategory,
        string sourceSubcategory,
        string query)
    {
        var context = new PublicMarketContextDto(
            "wildberries",
            sourceCategory,
            sourceSubcategory,
            query,
            PublicMarketIntelligenceContextCatalog.SourceRegionDest,
            PublicMarketIntelligenceContextCatalog.Sort,
            PublicMarketIntelligenceContextCatalog.TopN);

        return new PublicMarketIntelligenceDto(
            context,
            new ObservationWindowDto(
                "rank-run-1",
                null,
                null,
                null,
                DateTime.UtcNow,
                null,
                false,
                "complete",
                []),
            [],
            [],
            new PublicMarketPromoPressureDto([], []),
            new PublicMarketPricePressureDto([], [], []),
            new PublicMarketStockPressureDto(0, 0, 0, 0, [], [], []),
            new ConcentrationSummaryDto([], [], [], []),
            new MarketConcentrationDto(1, 1, 1, 100, 100, 1, 100, [], [], [], [], string.Empty, []),
            new PriceQualityMapDto(
                [],
                new PriceQualityMapSummaryDto(1, 0, 0, 0, 0, 1, null, null, string.Empty),
                []),
            new PriceCorridorsDto(0, null, null, null, null, null, null, null, null, null, null, [], string.Empty, []),
            []);
    }

    private sealed class FakeMarketIntelligenceReadService : IPublicMarketIntelligenceReadService
    {
        public List<PublicMarketIntelligenceQuery> Requests { get; } = [];

        public Task<ServiceResult<PublicMarketIntelligenceDto>> GetAsync(
            PublicMarketIntelligenceQuery query,
            CancellationToken cancellationToken)
        {
            Requests.Add(query);
            return Task.FromResult(ServiceResult<PublicMarketIntelligenceDto>.Success(BuildDto(
                query.SourceCategory!,
                query.SourceSubcategory!,
                query.Query!)));
        }
    }

    private sealed class MarketIntelligenceRefreshTestDbContext : ApplicationDbContext
    {
        public MarketIntelligenceRefreshTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(ParserRankSnapshotRow),
                typeof(PublicMarketIntelligenceSnapshot),
                typeof(PublicMarketConcentrationSnapshot)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (!allowedTypes.Contains(entityType))
                    modelBuilder.Ignore(entityType);
            }

            modelBuilder.Entity<ParserRankSnapshotRow>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<PublicMarketIntelligenceSnapshot>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<PublicMarketConcentrationSnapshot>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
