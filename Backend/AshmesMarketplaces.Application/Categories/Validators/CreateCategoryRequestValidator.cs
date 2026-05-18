using AshmesMarketplaces.Domain.Shared;
using AshmesMarketplaces.Application.Categories.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Categories.Validators;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    private const int NameMaxLength = 255;

    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.IdParentCategory)
            .NotEqual(Guid.Empty)
            .When(x => x.IdParentCategory.HasValue);

        RuleFor(x => x.IdOnMp)
            .MaximumLength(Constants.EXTERNAL_ID_MAX_LENGTH);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(NameMaxLength);

        RuleFor(x => x.Level)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.DateUpdate)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage("DateUpdate must be UTC.");
    }
}
