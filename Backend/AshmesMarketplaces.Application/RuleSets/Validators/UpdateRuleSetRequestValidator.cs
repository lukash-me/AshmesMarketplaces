using AshmesMarketplaces.Application.RuleSets.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RuleSets.Validators;

public sealed class UpdateRuleSetRequestValidator : AbstractValidator<UpdateRuleSetRequest>
{
    private const int NameMaxLength = 255;

    public UpdateRuleSetRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(x => x.DateCreate).Must(BeUtc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateUpdate).Must(BeUtc).WithMessage("DateUpdate must be UTC.");
        RuleFor(x => x.DateUpdate)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .WithMessage("DateUpdate cannot be earlier than DateCreate.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
