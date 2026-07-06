using System.Net;
using AshmesMarketplaces.Application.MarketplaceCategories.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.MarketplaceCategories;

public sealed class WildberriesCategoryCatalogServiceTests
{
    [Fact]
    public async Task RefreshAsync_upserts_leaf_categories_and_search_reads_database()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var refresh = await service.RefreshAsync(CancellationToken.None);
        var secondRefresh = await service.RefreshAsync(CancellationToken.None);
        var search = await service.SearchAsync(new("мужская"), CancellationToken.None);

        Assert.True(refresh.IsSuccess);
        Assert.True(secondRefresh.IsSuccess);
        Assert.Equal(2, await context.WildberriesCategoryLeaves.CountAsync());
        var leaf = Assert.Single(search.Value!, x => x.Id == 8194);
        Assert.Equal("Обувь / Мужская / Кеды и кроссовки", leaf.Path);
    }

    private static WildberriesCategoryCatalogService CreateService(ApplicationDbContext context)
    {
        var httpClient = new HttpClient(new FixtureHandler())
        {
            BaseAddress = new Uri("https://example.local")
        };
        return new WildberriesCategoryCatalogService(context, httpClient);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"wb-category-catalog-{Guid.NewGuid()}")
            .Options;

        return new CategoryCatalogTestDbContext(options);
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            const string json = """
            [
              {
                "id": 1,
                "name": "Женщинам",
                "childs": [
                  {
                    "id": 8137,
                    "name": "Платья и сарафаны",
                    "searchQuery": "menu_v3_8137 платье женские"
                  }
                ]
              },
              {
                "id": 2,
                "name": "Обувь",
                "childs": [
                  {
                    "id": 3,
                    "name": "Мужская",
                    "childs": [
                      {
                        "id": 8194,
                        "name": "Кеды и кроссовки",
                        "searchQuery": "menu_redirect_subject_v2_8194 мужские кеды и кроссовки"
                      }
                    ]
                  }
                ]
              }
            ]
            """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }

    private sealed class CategoryCatalogTestDbContext : ApplicationDbContext
    {
        public CategoryCatalogTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (entityType != typeof(WildberriesCategoryLeaf))
                    modelBuilder.Ignore(entityType);
            }

            modelBuilder.Entity<WildberriesCategoryLeaf>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.HasIndex(x => x.WbCategoryId).IsUnique();
            });
        }
    }
}
