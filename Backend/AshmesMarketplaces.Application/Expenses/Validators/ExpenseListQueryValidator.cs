using AshmesMarketplaces.Application.Expenses.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Expenses.Validators;

public sealed class ExpenseListQueryValidator : AbstractValidator<ExpenseListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "-name",
        "cost",
        "-cost",
        "datePay",
        "-datePay",
        "dateCreate",
        "-dateCreate",
        "dateUpdate",
        "-dateUpdate"
    };

    public ExpenseListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: name, -name, cost, -cost, datePay, -datePay, dateCreate, -dateCreate, dateUpdate, -dateUpdate.");
        RuleFor(x => x.IdWorkspace).NotEqual(Guid.Empty).When(x => x.IdWorkspace.HasValue);
        RuleFor(x => x.IdCategory).NotEqual(Guid.Empty).When(x => x.IdCategory.HasValue);
        RuleFor(x => x.IdCreator).NotEqual(Guid.Empty).When(x => x.IdCreator.HasValue);
        RuleFor(x => x.IdResponsible).NotEqual(Guid.Empty).When(x => x.IdResponsible.HasValue);
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0).When(x => x.Status.HasValue);
        RuleFor(x => x.DatePayFrom).Must(BeUtc).WithMessage("DatePayFrom must be UTC.");
        RuleFor(x => x.DatePayTo).Must(BeUtc).WithMessage("DatePayTo must be UTC.");
        RuleFor(x => x.DateCreateFrom).Must(BeUtc).WithMessage("DateCreateFrom must be UTC.");
        RuleFor(x => x.DateCreateTo).Must(BeUtc).WithMessage("DateCreateTo must be UTC.");
        RuleFor(x => x.DatePayTo)
            .GreaterThanOrEqualTo(x => x.DatePayFrom)
            .When(x => x.DatePayFrom.HasValue && x.DatePayTo.HasValue)
            .WithMessage("DatePayTo cannot be earlier than DatePayFrom.");
        RuleFor(x => x.DateCreateTo)
            .GreaterThanOrEqualTo(x => x.DateCreateFrom)
            .When(x => x.DateCreateFrom.HasValue && x.DateCreateTo.HasValue)
            .WithMessage("DateCreateTo cannot be earlier than DateCreateFrom.");
    }

    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
