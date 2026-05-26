using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.MarketRecommendations.Validators;

public sealed class RecalculateHotProductsRequestValidator : AbstractValidator<RecalculateHotProductsRequest>
{
    public RecalculateHotProductsRequestValidator()
    {
        RuleFor(x => x.MaxProducts)
            .GreaterThan(0)
            .When(x => x.MaxProducts.HasValue);

        RuleFor(x => x.MaxRecommendations)
            .GreaterThan(0)
            .LessThanOrEqualTo(200)
            .When(x => x.MaxRecommendations.HasValue);

        RuleFor(x => x.MinConfidence)
            .InclusiveBetween(0m, 1m)
            .When(x => x.MinConfidence.HasValue);

        RuleFor(x => x.MinProductsForScoring)
            .GreaterThanOrEqualTo(5)
            .When(x => x.MinProductsForScoring.HasValue);
    }
}
