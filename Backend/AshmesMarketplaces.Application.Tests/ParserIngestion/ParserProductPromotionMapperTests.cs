using System.Text.Json;
using AshmesMarketplaces.Application.ParserIngestion.Services;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Entities.Product;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserIngestion;

public sealed class ParserProductPromotionMapperTests
{
    [Fact]
    public void CreateDomainProduct_UsesMarketplaceExternalIdAndLeavesOwnerFieldsEmpty()
    {
        var marketplaceId = Guid.NewGuid();
        using var row = ProductRow(
            wbProductId: "123456789",
            skuProduct: "SKU-123",
            name: "Parser product",
            imageUrls: ["https://cdn.example/image-1.webp"]);

        var product = ParserProductPromotionMapper.CreateDomainProduct(row, marketplaceId);

        Assert.Equal(marketplaceId, product.IdMp);
        Assert.Equal("123456789", product.IdOnMp);
        Assert.Equal("SKU-123", product.SkuProduct);
        Assert.Equal("123456789", product.SkuSeller);
        Assert.Equal("Parser product", product.Name);
        Assert.Equal(ProductStatus.Active, product.Status);
        Assert.Equal(row.ParsedAtUtc, product.DateCreated);
        Assert.Equal(row.ParsedAtUtc, product.DateUpdated);
        Assert.Null(product.IdWorkspace);
        Assert.Null(product.IdUser);
        Assert.Single(product.Images);
    }

    [Fact]
    public void CreateDomainProduct_FallsBackSkuProductToMarketplaceId_WhenParserSkuIsMissing()
    {
        var marketplaceId = Guid.NewGuid();
        using var row = ProductRow(wbProductId: "555", skuProduct: null, name: "No SKU", imageUrls: []);

        var product = ParserProductPromotionMapper.CreateDomainProduct(row, marketplaceId);

        Assert.Equal("555", product.SkuProduct);
        Assert.Equal("555", product.SkuSeller);
    }

    [Fact]
    public void ApplyParserOwnedUpdates_UsesParserParsedAtAsDateUpdated()
    {
        var marketplaceId = Guid.NewGuid();
        using var initialRow = ProductRow(
            wbProductId: "777",
            skuProduct: "old-sku",
            name: "Old name",
            imageUrls: []);
        var product = ParserProductPromotionMapper.CreateDomainProduct(initialRow, marketplaceId);
        using var updateRow = ProductRow(
            wbProductId: "777",
            skuProduct: "new-sku",
            name: "New name",
            imageUrls: ["https://cdn.example/new.webp"],
            parsedAtUtc: new DateTime(2026, 06, 16, 13, 23, 00, DateTimeKind.Utc));

        ParserProductPromotionMapper.ApplyParserOwnedUpdates(product, updateRow);

        Assert.Equal("New name", product.Name);
        Assert.Equal("new-sku", product.SkuProduct);
        Assert.Equal(updateRow.ParsedAtUtc, product.DateUpdated);
        Assert.Contains(product.Images, image => image.Url == "https://cdn.example/new.webp");
    }

    private static ParserProductRow ProductRow(
        string wbProductId,
        string? skuProduct,
        string name,
        string[] imageUrls,
        DateTime? parsedAtUtc = null)
    {
        return new ParserProductRow(
            idParserRun: Guid.NewGuid(),
            idParserFile: Guid.NewGuid(),
            sourceLineNumber: 1,
            rowHash: "hash",
            schemaVersion: 1,
            marketplace: "wildberries",
            parserRunId: "parser-run",
            parsedAtUtc: parsedAtUtc ?? new DateTime(2026, 06, 15, 10, 00, 00, DateTimeKind.Utc),
            sourceCategory: "Дом",
            sourceSubcategory: "Светильники бра",
            sourceQuery: "бра",
            sourceRegionDest: "12354108",
            wbProductId: wbProductId,
            skuProduct: skuProduct,
            name: name,
            entity: null,
            brandIdOnMp: null,
            brandName: null,
            sellerIdOnMp: null,
            sellerName: null,
            priceRegular: 1000,
            priceDiscounted: 900,
            priceWbWallet: 850,
            discountPercent: 10,
            totalQuantity: 12,
            ratingRounded: 5,
            reviewRating: 4.8m,
            feedbackCount: 42,
            feedbackCountSource: "wb",
            imageUrls: JsonDocument.Parse(JsonSerializer.Serialize(imageUrls)),
            imageCount: imageUrls.Length,
            wbRootId: "root",
            subjectParentId: null,
            subjectId: null,
            rawObservedFields: null);
    }
}
