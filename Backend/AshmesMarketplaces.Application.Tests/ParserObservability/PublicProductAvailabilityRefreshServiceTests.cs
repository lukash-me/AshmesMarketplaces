using System.Text.Json;
using AshmesMarketplaces.Application.ParserObservability.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserObservability;

public sealed class PublicProductAvailabilityRefreshServiceTests
{
    [Fact]
    public async Task RefreshIncludesAllEligibleProductsWithoutHundredItemCap()
    {
        await using var context = CreateContext();
        var productRun = ParserRun("products-run", "products", Utc(2026, 7, 4, 8));
        var logisticsRun = ParserRun("logistics-run", "logistics", Utc(2026, 7, 4, 9));
        var proxyRun = CompletedProxyRun("cycle-1", "proxy-1", Utc(2026, 7, 4, 10));
        context.ParserRuns.AddRange(productRun, logisticsRun);
        context.ParserProxyRuns.Add(proxyRun);

        for (var index = 1; index <= 250; index++)
        {
            var wbProductId = index.ToString("0000");
            var productRow = ProductRow(productRun, wbProductId, index);
            context.ParserProductRows.Add(productRow);
            context.ParserCurrentProductRows.Add(CurrentProductRow(productRow));
            context.ParserLogisticsSnapshotRows.Add(LogisticsRow(logisticsRun, wbProductId, index));
            context.ParserRunProductEffects.Add(ProductEffect(proxyRun, wbProductId));
        }

        await context.SaveChangesAsync();

        var result = await CreateService(context).RefreshAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(250, result.Value!.TotalCount);
        var snapshot = await context.PublicProductAvailabilitySnapshots.SingleAsync();
        Assert.Equal(250, snapshot.TotalCount);
        Assert.Equal(250, CountSnapshotItems(snapshot.ItemsJson));
    }

    [Fact]
    public async Task RefreshIncludesCurrentProductsWithLogisticsAcrossMultipleRuns()
    {
        await using var context = CreateContext();
        var productRun = ParserRun("products-run", "products", Utc(2026, 7, 4, 8));
        var logisticsRuns = Enumerable.Range(1, 3)
            .Select(index => ParserRun($"logistics-run-{index}", "logistics", Utc(2026, 7, 4, 9, index)))
            .ToList();
        context.ParserRuns.Add(productRun);
        context.ParserRuns.AddRange(logisticsRuns);

        for (var index = 1; index <= 300; index++)
        {
            var wbProductId = index.ToString("0000");
            var productRow = ProductRow(productRun, wbProductId, index);
            var logisticsRun = logisticsRuns[(index - 1) / 100];
            context.ParserProductRows.Add(productRow);
            context.ParserCurrentProductRows.Add(CurrentProductRow(productRow));
            context.ParserLogisticsSnapshotRows.Add(LogisticsRow(logisticsRun, wbProductId, index));
        }

        await context.SaveChangesAsync();

        var result = await CreateService(context).RefreshAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(300, result.Value!.TotalCount);
        var snapshot = await context.PublicProductAvailabilitySnapshots.SingleAsync();
        Assert.Equal(300, snapshot.TotalCount);
        Assert.Equal(300, CountSnapshotItems(snapshot.ItemsJson));
    }

    [Fact]
    public async Task RefreshExcludesCurrentProductsWithoutLogisticsProfile()
    {
        await using var context = CreateContext();
        var productRun = ParserRun("products-run", "products", Utc(2026, 7, 4, 8));
        var logisticsRun = ParserRun("logistics-run", "logistics", Utc(2026, 7, 4, 9));
        context.ParserRuns.AddRange(productRun, logisticsRun);

        var withLogistics = ProductRow(productRun, "with-logistics", 1);
        var withoutLogistics = ProductRow(productRun, "without-logistics", 2);
        context.ParserProductRows.AddRange(withLogistics, withoutLogistics);
        context.ParserCurrentProductRows.AddRange(CurrentProductRow(withLogistics), CurrentProductRow(withoutLogistics));
        context.ParserLogisticsSnapshotRows.Add(LogisticsRow(logisticsRun, "with-logistics", 1));
        await context.SaveChangesAsync();

        var result = await CreateService(context).RefreshAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, result.Value!.TotalCount);
        var snapshot = await context.PublicProductAvailabilitySnapshots.SingleAsync();
        Assert.Contains("\"wbProductId\":\"with-logistics\"", snapshot.ItemsJson);
        Assert.DoesNotContain("\"wbProductId\":\"without-logistics\"", snapshot.ItemsJson);
    }

    [Fact]
    public async Task RefreshUsesSourceProductImageWhenCurrentImageIsMissing()
    {
        await using var context = CreateContext();
        var productRun = ParserRun("products-run", "products", Utc(2026, 7, 4, 8));
        var logisticsRun = ParserRun("logistics-run", "logistics", Utc(2026, 7, 4, 9));
        context.ParserRuns.AddRange(productRun, logisticsRun);

        var productRow = ProductRow(productRun, "1002662091", 1);
        var currentRow = CurrentProductRow(productRow);
        ClearCurrentImageUrls(currentRow);
        context.ParserProductRows.Add(productRow);
        context.ParserCurrentProductRows.Add(currentRow);
        context.ParserLogisticsSnapshotRows.Add(LogisticsRow(logisticsRun, "1002662091", 1));
        await context.SaveChangesAsync();
        Assert.Contains(
            "https://images.example/product.jpg",
            (await context.ParserProductRows.SingleAsync()).ImageUrls?.RootElement.GetRawText());
        Assert.Single(await context.ParserProductRows.Where(x => x.WbProductId == "1002662091").ToListAsync());

        var result = await CreateService(context).RefreshAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = await context.PublicProductAvailabilitySnapshots.SingleAsync();
        using var document = JsonDocument.Parse(snapshot.ItemsJson);
        Assert.Equal(
            "https://basket-41.wbbasket.ru/vol10026/part1002662/1002662091/images/big/1.webp",
            document.RootElement[0].GetProperty("thumbnailUrl").GetString());
    }

    private static PublicProductAvailabilityRefreshService CreateService(ApplicationDbContext context) =>
        new(context);

    private static int CountSnapshotItems(string itemsJson)
    {
        using var document = JsonDocument.Parse(itemsJson);
        return document.RootElement.GetArrayLength();
    }

    private static ParserRun ParserRun(string parserRunId, string kind, DateTime startedAtUtc) =>
        new(
            parserRunId,
            "wildberries",
            kind,
            "succeeded",
            1,
            null,
            startedAtUtc,
            startedAtUtc.AddMinutes(5),
            null,
            null,
            startedAtUtc.AddMinutes(6));

    private static ParserProxyRun CompletedProxyRun(string cycleId, string proxyKey, DateTime startedAtUtc)
    {
        var run = new ParserProxyRun(
            "parser-local-01",
            $"{cycleId}:{proxyKey}",
            cycleId,
            ParserProxyRunCycleKinds.Production,
            proxyKey,
            "Женщинам",
            "Платья и сарафаны",
            100,
            0,
            null,
            null,
            null,
            startedAtUtc);
        run.Complete(100, 100, startedAtUtc.AddMinutes(5));
        return run;
    }

    private static ParserProductRow ProductRow(ParserRun run, string wbProductId, long sourceLineNumber) =>
        new(
            run.Id,
            Guid.NewGuid(),
            sourceLineNumber,
            $"product-{wbProductId}",
            1,
            "wildberries",
            run.ParserRunId,
            run.StartedAtUtc.AddMinutes(sourceLineNumber),
            "Женщинам",
            "Платья и сарафаны",
            "Платья и сарафаны",
            "12354108",
            wbProductId,
            null,
            $"Товар {wbProductId}",
            null,
            10,
            "Brand",
            20,
            "Seller",
            1000,
            900,
            850,
            10,
            5,
            5,
            4.8m,
            100,
            "wb",
            JsonDocument.Parse("""["https://images.example/product.jpg"]"""),
            1,
            $"root-{wbProductId}",
            1,
            2,
            null);

    private static ParserLogisticsSnapshotRow LogisticsRow(ParserRun run, string wbProductId, long sourceLineNumber) =>
        new(
            run.Id,
            Guid.NewGuid(),
            sourceLineNumber,
            $"logistics-{wbProductId}",
            1,
            run.ParserRunId,
            "wildberries",
            run.StartedAtUtc.AddMinutes(sourceLineNumber),
            "card",
            "wb-logistics",
            $"request-{wbProductId}",
            "12354108",
            $"profile-{wbProductId}",
            "Москва",
            "v1",
            "Москва",
            "Москва",
            null,
            null,
            null,
            "Женщинам",
            "Платья и сарафаны",
            "Платья и сарафаны",
            wbProductId,
            $"root-{wbProductId}",
            "20",
            "Seller",
            5,
            false,
            null,
            "exact",
            null,
            1,
            2,
            null,
            null,
            "calculated",
            "Завтра",
            run.StartedAtUtc.Date.AddDays(1),
            "wb",
            run.StartedAtUtc,
            null,
            null);

    private static ParserRunProductEffect ProductEffect(ParserProxyRun run, string wbProductId) =>
        new(
            run.Id,
            Guid.NewGuid(),
            wbProductId,
            Guid.NewGuid(),
            ParserRunProductEffectTypes.Updated,
            run.StartedAtUtc.AddMinutes(1));

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        DateTime.SpecifyKind(new DateTime(year, month, day, hour, minute, 0), DateTimeKind.Utc);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"public-product-availability-{Guid.NewGuid()}")
            .Options;
        return new PublicProductAvailabilityTestDbContext(options);
    }

    private sealed class PublicProductAvailabilityTestDbContext : ApplicationDbContext
    {
        public PublicProductAvailabilityTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var keptTypes = new HashSet<Type>
            {
                typeof(PublicProductAvailabilitySnapshot),
                typeof(ParserRun),
                typeof(ParserProductRow),
                typeof(ParserCurrentProductRow),
                typeof(ParserLogisticsSnapshotRow),
                typeof(ParserWarehouseAvailabilityRow),
                typeof(ParserProxyRun),
                typeof(ParserRunProductEffect),
                typeof(ParserRankSnapshotRow),
                typeof(ParserReviewRow),
                typeof(ParserReviewReplyRow),
                typeof(ParserReviewRootFetch),
                typeof(ParserCurrentProductReviewsSummary)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
            {
                if (!keptTypes.Contains(entityType.ClrType))
                    modelBuilder.Ignore(entityType.ClrType);
            }

            foreach (var entityType in keptTypes)
            {
                modelBuilder.Entity(entityType).HasKey("Id");
                modelBuilder.Entity(entityType).Property("Id").ValueGeneratedNever();
            }
        }
    }

    private static ParserCurrentProductRow CurrentProductRow(ParserProductRow row) =>
        new(
            row,
            new ProductGroupHashes(
                $"identity-{row.WbProductId}",
                $"price-{row.WbProductId}",
                $"stock-{row.WbProductId}",
                $"rating-{row.WbProductId}",
                $"reviews-{row.WbProductId}",
                $"media-{row.WbProductId}",
                $"seller-brand-{row.WbProductId}"),
            row.ParsedAtUtc);

    private static void ClearCurrentImageUrls(ParserCurrentProductRow row)
    {
        typeof(ParserCurrentProductRow)
            .GetProperty(nameof(ParserCurrentProductRow.ImageUrlsJson))!
            .SetValue(row, null);
    }
}
