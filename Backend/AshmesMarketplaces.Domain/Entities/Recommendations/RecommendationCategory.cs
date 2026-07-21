namespace AshmesMarketplaces.Domain.Entities.Recommendations;

public class RecommendationCategory
{
    private RecommendationCategory() { }

    public RecommendationCategory(Guid idRecommendation, Guid idCategory)
    {
        if (idRecommendation == Guid.Empty)
            throw new ArgumentException("Recommendation id is required", nameof(idRecommendation));

        if (idCategory == Guid.Empty)
            throw new ArgumentException("Category id is required", nameof(idCategory));

        IdRecommendation = idRecommendation;
        IdCategory = idCategory;
    }

    public Guid IdRecommendation { get; private set; }
    public Guid IdCategory { get; private set; }
}
