using System.Text.Json;
using AshmesMarketplaces.Application.ParserObservability.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserObservability;

public sealed class ParserProductReadServiceTests
{
    [Fact]
    public async Task GetCoveredNichesReturnsOnlyNichesWithCurrentProducts()
    {
        await using var context = CreateContext();
        context.WildberriesCategoryLeaves.AddRange(
            CategoryLeaf(8194, "Кеды и кроссовки", "Обувь", "Кеды и кроссовки", "Обувь / Мужская / Кеды и кроссовки", "menu_redirect_subject_v2_8194 мужские кеды и кроссовки"),
            CategoryLeaf(130752, "Очистители автомобильные", "Автотовары", "Очистители автомобильные", "Автотовары / Автокосметика и автохимия / Очистители автомобильные", "menu_v3_130752 очиститель машины"));
        context.ParserCurrentProductRows.AddRange(
            CurrentProduct("1001", "Обувь", "Кеды и кроссовки", "мужские кеды и кроссовки"),
            CurrentProduct("1002", "Обувь", "Кеды и кроссовки", "мужские кеды и кроссовки"));
        await context.SaveChangesAsync();

        var result = await new ParserProductReadService(context).GetCoveredNichesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var item = Assert.Single(result.Value!);
        Assert.Equal(8194, item.WbCategoryId);
        Assert.Equal("Обувь / Мужская / Кеды и кроссовки", item.SourcePath);
        Assert.Equal(2, item.ProductsCount);
    }

    [Fact]
    public async Task GetCoveredNichesUsesLegacyPathWhenCategoryScopeIsAmbiguous()
    {
        await using var context = CreateContext();
        context.WildberriesCategoryLeaves.AddRange(
            CategoryLeaf(8194, "Кеды и кроссовки", "Обувь", "Кеды и кроссовки", "Обувь / Мужская / Кеды и кроссовки", "menu_redirect_subject_v2_8194 мужские кеды и кроссовки"),
            CategoryLeaf(8123, "Кеды и кроссовки", "Обувь", "Кеды и кроссовки", "Обувь / Женская / Кеды и кроссовки", "menu_redirect_subject_v2_8123 женские кеды и кроссовки"));
        context.ParserCurrentProductRows.Add(CurrentProduct("1001", "Обувь", "Кеды и кроссовки", null));
        await context.SaveChangesAsync();

        var result = await new ParserProductReadService(context).GetCoveredNichesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var item = Assert.Single(result.Value!);
        Assert.Null(item.WbCategoryId);
        Assert.Equal("Обувь / Кеды и кроссовки", item.SourcePath);
        Assert.Equal(1, item.ProductsCount);
    }

    [Fact]
    public async Task GetCoveredNichesUsesActiveProxyAssignmentForLegacyShortQuery()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 7, 8, 10);
        context.WildberriesCategoryLeaves.AddRange(
            CategoryLeaf(8194, "Кеды и кроссовки", "Обувь", "Кеды и кроссовки", "Обувь / Мужская / Кеды и кроссовки", "menu_redirect_subject_v2_8194 мужские кеды и кроссовки"),
            CategoryLeaf(8123, "Кеды и кроссовки", "Обувь", "Кеды и кроссовки", "Обувь / Женская / Кеды и кроссовки", "menu_redirect_subject_v2_8123 женские кеды и кроссовки"));
        context.ParserProxyNicheAssignments.Add(new ParserProxyNicheAssignment(
            Guid.NewGuid(),
            8194,
            "Обувь",
            "Кеды и кроссовки",
            "Обувь / Мужская / Кеды и кроссовки",
            "menu_redirect_subject_v2_8194 мужские кеды и кроссовки",
            "Кеды и кроссовки",
            true,
            now));
        context.ParserCurrentProductRows.Add(CurrentProduct("1001", "Обувь", "Кеды и кроссовки", "Кеды и кроссовки"));
        await context.SaveChangesAsync();

        var result = await new ParserProductReadService(context).GetCoveredNichesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var item = Assert.Single(result.Value!);
        Assert.Equal(8194, item.WbCategoryId);
        Assert.Equal("Обувь / Мужская / Кеды и кроссовки", item.SourcePath);
        Assert.Equal(1, item.ProductsCount);
    }

    [Fact]
    public async Task GetCoveredNichesResolvesAmbiguousCategoryByHumanQuery()
    {
        await using var context = CreateContext();
        context.WildberriesCategoryLeaves.AddRange(
            CategoryLeaf(8194, "Кеды и кроссовки", "Обувь", "Кеды и кроссовки", "Обувь / Мужская / Кеды и кроссовки", "menu_redirect_subject_v2_8194 мужские кеды и кроссовки"),
            CategoryLeaf(8123, "Кеды и кроссовки", "Обувь", "Кеды и кроссовки", "Обувь / Женская / Кеды и кроссовки", "menu_redirect_subject_v2_8123 женские кеды и кроссовки"));
        context.ParserCurrentProductRows.Add(CurrentProduct("1001", "Обувь", "Кеды и кроссовки", "мужские кеды и кроссовки"));
        await context.SaveChangesAsync();

        var result = await new ParserProductReadService(context).GetCoveredNichesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var item = Assert.Single(result.Value!);
        Assert.Equal(8194, item.WbCategoryId);
        Assert.Equal("Обувь / Мужская / Кеды и кроссовки", item.SourcePath);
    }

    private static WildberriesCategoryLeaf CategoryLeaf(
        long wbCategoryId,
        string name,
        string sourceCategory,
        string sourceSubcategory,
        string sourcePath,
        string searchQuery)
    {
        var now = Utc(2026, 7, 8, 10);
        return new WildberriesCategoryLeaf(
            wbCategoryId,
            name,
            sourceCategory,
            sourceSubcategory,
            sourcePath,
            searchQuery,
            null,
            2,
            now,
            now);
    }

    private static ParserCurrentProductRow CurrentProduct(
        string wbProductId,
        string category,
        string subcategory,
        string? sourceQuery)
    {
        var row = new ParserProductRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            $"hash-{wbProductId}",
            1,
            "wildberries",
            "products-run",
            Utc(2026, 7, 8, 10),
            category,
            subcategory,
            sourceQuery,
            "12354108",
            wbProductId,
            null,
            $"Product {wbProductId}",
            null,
            10,
            "Brand",
            20,
            "Seller",
            110,
            100,
            95,
            10,
            5,
            5,
            4.9m,
            11,
            "wb",
            JsonDocument.Parse("""["https://images.example/product.jpg"]"""),
            1,
            $"root-{wbProductId}",
            1,
            2,
            null);
        return new ParserCurrentProductRow(
            row,
            new ProductGroupHashes(
                $"identity-{wbProductId}",
                $"price-{wbProductId}",
                $"stock-{wbProductId}",
                $"rating-{wbProductId}",
                $"reviews-{wbProductId}",
                $"media-{wbProductId}",
                $"seller-{wbProductId}"),
            Utc(2026, 7, 8, 10));
    }

    private static DateTime Utc(int year, int month, int day, int hour) =>
        DateTime.SpecifyKind(new DateTime(year, month, day, hour, 0, 0), DateTimeKind.Utc);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-product-read-{Guid.NewGuid()}")
            .Options;
        return new ParserProductReadTestDbContext(options);
    }

    private sealed class ParserProductReadTestDbContext : ApplicationDbContext
    {
        public ParserProductReadTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(ParserCurrentProductRow),
                typeof(WildberriesCategoryLeaf),
                typeof(ParserProxyNicheAssignment)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (!allowedTypes.Contains(entityType))
                    modelBuilder.Ignore(entityType);
            }

            modelBuilder.Entity<ParserCurrentProductRow>().HasKey(x => x.Id);
            modelBuilder.Entity<WildberriesCategoryLeaf>().HasKey(x => x.Id);
            modelBuilder.Entity<ParserProxyNicheAssignment>().HasKey(x => x.Id);
        }
    }
}
