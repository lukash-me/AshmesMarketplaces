using AshmesMarketplaces.Application.Expenses.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Expenses.Validators;

public sealed class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseRequest>
{
    private const int NameMaxLength = 255;

    public CreateExpenseRequestValidator()
    {
        RuleFor(x => x.IdWorkspace).NotEmpty();
        RuleFor(x => x.IdCategory).NotEqual(Guid.Empty).When(x => x.IdCategory.HasValue);
        RuleFor(x => x.IdCreator).NotEmpty();
        RuleFor(x => x.IdResponsible).NotEqual(Guid.Empty).When(x => x.IdResponsible.HasValue);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DatePay).Must(BeUtc).WithMessage("DatePay must be UTC.");
        RuleFor(x => x.DateCreate).Must(BeUtc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateUpdate).Must(BeUtc).WithMessage("DateUpdate must be UTC.");
        RuleFor(x => x.DatePay)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .When(x => x.DatePay.HasValue)
            .WithMessage("DatePay cannot be earlier than DateCreate.");
        RuleFor(x => x.DateUpdate)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .WithMessage("DateUpdate cannot be earlier than DateCreate.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
