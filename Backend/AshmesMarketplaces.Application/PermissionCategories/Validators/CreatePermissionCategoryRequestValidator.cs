using AshmesMarketplaces.Application.PermissionCategories.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.PermissionCategories.Validators;

public sealed class CreatePermissionCategoryRequestValidator : AbstractValidator<CreatePermissionCategoryRequest>
{
    private const int NameMaxLength = 255;

    public CreatePermissionCategoryRequestValidator()
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
