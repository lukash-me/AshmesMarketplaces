using AshmesMarketplaces.Application.Rules.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Rules.Validators;

public sealed class CreateRuleRequestValidator : AbstractValidator<CreateRuleRequest>
{
    private const int NameMaxLength = 255;
    private const int CodeMaxLength = 128;

    public CreateRuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodeMaxLength);
        RuleFor(x => x.Number).GreaterThanOrEqualTo(0).When(x => x.Number.HasValue);
        RuleFor(x => x.Domain).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DateCreate).Must(BeUtc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateUpdate).Must(BeUtc).WithMessage("DateUpdate must be UTC.");
        RuleFor(x => x.DateUpdate)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .WithMessage("DateUpdate cannot be earlier than DateCreate.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
