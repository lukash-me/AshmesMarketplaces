using AshmesMarketplaces.Application.RecommendationProducts.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RecommendationProducts.Validators;

public sealed class CreateRecommendationProductRequestValidator : AbstractValidator<CreateRecommendationProductRequest>
{
    public CreateRecommendationProductRequestValidator()
    {
        RuleFor(x => x.IdRecommendation).NotEmpty();
        RuleFor(x => x.IdProduct).NotEmpty();
    }
}
