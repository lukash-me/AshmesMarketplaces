using AshmesMarketplaces.Application.RecommendationCategories.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RecommendationCategories.Validators;

public sealed class RecommendationCategoryListQueryValidator : AbstractValidator<RecommendationCategoryListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "idRecommendation",
        "-idRecommendation",
        "idCategory",
        "-idCategory"
    };

    public RecommendationCategoryListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: idRecommendation, -idRecommendation, idCategory, -idCategory.");
        RuleFor(x => x.IdRecommendation).NotEqual(Guid.Empty).When(x => x.IdRecommendation.HasValue);
        RuleFor(x => x.IdCategory).NotEqual(Guid.Empty).When(x => x.IdCategory.HasValue);
    }
}
