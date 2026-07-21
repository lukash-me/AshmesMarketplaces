using AshmesMarketplaces.Application.Recommendations.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Recommendations.Validators;

public sealed class UpdateRecommendationRequestValidator : AbstractValidator<UpdateRecommendationRequest>
{
    public UpdateRecommendationRequestValidator()
    {
        RuleFor(x => x.IdModel).NotEmpty();
        RuleFor(x => x.Type).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TypeObject).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Score).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DateCreate).Must(BeUtc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateUpdate).Must(BeUtc).WithMessage("DateUpdate must be UTC.");
        RuleFor(x => x.DateUpdate)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .WithMessage("DateUpdate cannot be earlier than DateCreate.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
