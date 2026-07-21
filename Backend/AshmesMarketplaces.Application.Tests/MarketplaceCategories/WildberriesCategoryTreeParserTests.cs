using System.Text.Json;
using AshmesMarketplaces.Application.MarketplaceCategories.Services;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.MarketplaceCategories;

public sealed class WildberriesCategoryTreeParserTests
{
    [Fact]
    public void Parse_exposes_human_search_query_without_wb_menu_token()
    {
        const string json = """
        [
          {
            "id": 1,
            "name": "Auto",
            "childs": [
              {
                "id": 130752,
                "name": "Car cleaners",
                "seo": "Vehicle cleaner",
                "searchQuery": "menu_v3_130752 car cleaner"
              }
            ]
          }
        ]
        """;

        using var document = JsonDocument.Parse(json);

        var node = Assert.Single(WildberriesCategoryTreeParser.Parse(document.RootElement), x => x.Id == 130752);

        Assert.Equal("menu_v3_130752 car cleaner", node.SearchQuery);
        Assert.Equal("car cleaner", node.HumanSearchQuery);
    }

    [Fact]
    public void Parse_builds_leaf_nodes_with_wb_name_path_and_search_query()
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
                "seo": "Женские платья и сарафаны",
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
                    "seo": "Мужские кеды и кроссовки",
                    "searchQuery": "menu_redirect_subject_v2_8194 мужские кеды и кроссовки"
                  }
                ]
              }
            ]
          }
        ]
        """;

        using var document = JsonDocument.Parse(json);

        var nodes = WildberriesCategoryTreeParser.Parse(document.RootElement);

        var dresses = Assert.Single(nodes, x => x.Id == 8137);
        Assert.True(dresses.IsLeaf);
        Assert.Equal("Платья и сарафаны", dresses.Name);
        Assert.Equal("Женщинам", dresses.SourceCategory);
        Assert.Equal("Платья и сарафаны", dresses.SourceSubcategory);
        Assert.Equal("Женщинам / Платья и сарафаны", dresses.Path);
        Assert.Equal("menu_v3_8137 платье женские", dresses.SearchQuery);

        var shoes = Assert.Single(nodes, x => x.Id == 8194);
        Assert.True(shoes.IsLeaf);
        Assert.Equal("Кеды и кроссовки", shoes.Name);
        Assert.Equal("Обувь", shoes.SourceCategory);
        Assert.Equal("Кеды и кроссовки", shoes.SourceSubcategory);
        Assert.Equal("Обувь / Мужская / Кеды и кроссовки", shoes.Path);
        Assert.Equal("menu_redirect_subject_v2_8194 мужские кеды и кроссовки", shoes.SearchQuery);
    }
}
