using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.MarketRecommendations.Validators;

public sealed class HotProductsListQueryValidator : AbstractValidator<HotProductsListQuery>
{
    public HotProductsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
