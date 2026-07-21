using AshmesMarketplaces.Application.RecommendationProducts.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RecommendationProducts.Validators;

public sealed class RecommendationProductListQueryValidator : AbstractValidator<RecommendationProductListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "idRecommendation",
        "-idRecommendation",
        "idProduct",
        "-idProduct"
    };

    public RecommendationProductListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: idRecommendation, -idRecommendation, idProduct, -idProduct.");
        RuleFor(x => x.IdRecommendation).NotEqual(Guid.Empty).When(x => x.IdRecommendation.HasValue);
        RuleFor(x => x.IdProduct).NotEqual(Guid.Empty).When(x => x.IdProduct.HasValue);
    }
}
