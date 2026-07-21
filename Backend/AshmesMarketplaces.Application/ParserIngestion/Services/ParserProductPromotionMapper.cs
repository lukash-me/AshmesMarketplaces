using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.IDs;

namespace AshmesMarketplaces.Application.ParserIngestion.Services;

public static class ParserProductPromotionMapper
{
    public static Product CreateDomainProduct(ParserProductRow row, Guid marketplaceId)
    {
        var skuProduct = string.IsNullOrWhiteSpace(row.SkuProduct) ? row.WbProductId : row.SkuProduct;
        var created = Product.Create(
            ProductId.NewId(),
            idSetPrice: null,
            idWorkspace: null,
            idBrand: null,
            idMp: marketplaceId,
            idUser: null,
            idCategory: null,
            idOnMp: row.WbProductId,
            skuProduct: skuProduct,
            skuSeller: row.WbProductId,
            name: row.Name,
            description: null,
            characteristicsJson: null,
            barcode: null,
            commission: null,
            status: ProductStatus.Active,
            dateUpdated: row.ParsedAtUtc,
            dateCreated: row.ParsedAtUtc);

        if (created.IsFailure)
            throw new InvalidOperationException(created.Error.Message);

        foreach (var imageUrl in ImageUrls(row))
            created.Value.AddImage(imageUrl);

        return created.Value;
    }

    public static void ApplyParserOwnedUpdates(Product product, ParserProductRow row)
    {
        var result = product.UpdateParserOwnedFields(row.Name, row.SkuProduct, row.ParsedAtUtc);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error.Message);

        foreach (var imageUrl in ImageUrls(row))
            product.AddImage(imageUrl);
    }

    public static IEnumerable<string> ImageUrls(ParserProductRow row)
    {
        if (row.ImageUrls?.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            yield break;

        foreach (var item in row.ImageUrls.RootElement.EnumerateArray())
        {
            var value = item.ValueKind == System.Text.Json.JsonValueKind.String ? item.GetString() : null;
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }
}
