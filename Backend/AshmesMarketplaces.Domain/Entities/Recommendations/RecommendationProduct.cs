using AshmesMarketplaces.Domain.IDs;

namespace AshmesMarketplaces.Domain.Entities.Recommendations;

public class RecommendationProduct
{
    private RecommendationProduct() { }

    public RecommendationProduct(Guid idRecommendation, ProductId idProduct)
    {
        if (idRecommendation == Guid.Empty)
            throw new ArgumentException("Recommendation id is required", nameof(idRecommendation));

        if (idProduct.Value == Guid.Empty)
            throw new ArgumentException("Product id is required", nameof(idProduct));

        IdRecommendation = idRecommendation;
        IdProduct = idProduct;
    }

    public Guid IdRecommendation { get; private set; }
    public ProductId IdProduct { get; private set; } = ProductId.EmptyId();
}
