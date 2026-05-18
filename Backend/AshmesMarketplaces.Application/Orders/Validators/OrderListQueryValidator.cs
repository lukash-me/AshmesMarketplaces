using AshmesMarketplaces.Application.Orders.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Orders.Validators;

public sealed class OrderListQueryValidator : AbstractValidator<OrderListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "dateOpened",
        "-dateOpened",
        "dateUpdate",
        "-dateUpdate",
        "price",
        "-price",
        "amount",
        "-amount"
    };

    public OrderListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: dateOpened, -dateOpened, dateUpdate, -dateUpdate, price, -price, amount, -amount.");
        RuleFor(x => x.IdProduct).NotEqual(Guid.Empty).When(x => x.IdProduct.HasValue);
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0).When(x => x.Status.HasValue);
        RuleFor(x => x.DateOpenedFrom).Must(BeUtc).WithMessage("DateOpenedFrom must be UTC.");
        RuleFor(x => x.DateOpenedTo).Must(BeUtc).WithMessage("DateOpenedTo must be UTC.");
        RuleFor(x => x.DateOpenedTo)
            .GreaterThanOrEqualTo(x => x.DateOpenedFrom)
            .When(x => x.DateOpenedFrom.HasValue && x.DateOpenedTo.HasValue)
            .WithMessage("DateOpenedTo cannot be earlier than DateOpenedFrom.");
    }

    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
