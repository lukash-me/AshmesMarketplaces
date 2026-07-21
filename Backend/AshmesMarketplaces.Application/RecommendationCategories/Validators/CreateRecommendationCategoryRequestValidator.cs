using AshmesMarketplaces.Application.RecommendationCategories.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RecommendationCategories.Validators;

public sealed class CreateRecommendationCategoryRequestValidator : AbstractValidator<CreateRecommendationCategoryRequest>
{
    public CreateRecommendationCategoryRequestValidator()
    {
        RuleFor(x => x.IdRecommendation).NotEmpty();
        RuleFor(x => x.IdCategory).NotEmpty();
    }
}
