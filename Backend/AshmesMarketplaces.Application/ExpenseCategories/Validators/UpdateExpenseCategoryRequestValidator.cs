using AshmesMarketplaces.Application.ExpenseCategories.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.ExpenseCategories.Validators;

public sealed class UpdateExpenseCategoryRequestValidator : AbstractValidator<UpdateExpenseCategoryRequest>
{
    private const int NameMaxLength = 255;

    public UpdateExpenseCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(x => x.DateCreate).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateUpdate).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("DateUpdate must be UTC.");
        RuleFor(x => x.DateUpdate)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .WithMessage("DateUpdate cannot be earlier than DateCreate.");
    }
}
