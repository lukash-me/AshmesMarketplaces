using AshmesMarketplaces.Application.Permissions.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Permissions.Validators;

public sealed class CreatePermissionRequestValidator : AbstractValidator<CreatePermissionRequest>
{
    private const int NameMaxLength = 255;

    public CreatePermissionRequestValidator()
    {
        RuleFor(x => x.IdCategory).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Domain).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DateCreate).Must(BeUtc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateUpdate).Must(BeUtc).WithMessage("DateUpdate must be UTC.");
        RuleFor(x => x.DateUpdate)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .WithMessage("DateUpdate cannot be earlier than DateCreate.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
