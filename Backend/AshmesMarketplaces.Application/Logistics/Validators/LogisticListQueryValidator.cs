using AshmesMarketplaces.Application.Logistics.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Logistics.Validators;

public sealed class LogisticListQueryValidator : AbstractValidator<LogisticListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "date",
        "-date",
        "stockAmountStatistic",
        "-stockAmountStatistic"
    };

    public LogisticListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: date, -date, stockAmountStatistic, -stockAmountStatistic.");
        RuleFor(x => x.IdProduct).NotEqual(Guid.Empty).When(x => x.IdProduct.HasValue);
        RuleFor(x => x.IdWarehouse).NotEqual(Guid.Empty).When(x => x.IdWarehouse.HasValue);
        RuleFor(x => x.Type).GreaterThanOrEqualTo(0).When(x => x.Type.HasValue);
        RuleFor(x => x.DateFrom).Must(BeUtc).WithMessage("DateFrom must be UTC.");
        RuleFor(x => x.DateTo).Must(BeUtc).WithMessage("DateTo must be UTC.");
        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("DateTo cannot be earlier than DateFrom.");
    }

    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
