using AshmesMarketplaces.Application.Brands.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Brands.Validators;

public sealed class UpdateBrandRequestValidator : AbstractValidator<UpdateBrandRequest>
{
    private const int NameMaxLength = 255;
    private const int ShortTextMaxLength = 128;

    public UpdateBrandRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(NameMaxLength);

        RuleFor(x => x.Country)
            .NotEmpty()
            .MaximumLength(ShortTextMaxLength);

        RuleFor(x => x.Manufacturer)
            .NotEmpty()
            .MaximumLength(NameMaxLength);

        RuleFor(x => x.SalesAmount)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.RateRedemption)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Level)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Type)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.DateMpRegistration)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage("DateMpRegistration must be UTC.");

        RuleFor(x => x.DateUpdate)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage("DateUpdate must be UTC.");
    }
}
