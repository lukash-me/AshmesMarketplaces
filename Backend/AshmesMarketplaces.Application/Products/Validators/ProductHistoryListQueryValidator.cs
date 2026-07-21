using AshmesMarketplaces.Application.Products.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Products.Validators;

public sealed class ProductHistoryListQueryValidator : AbstractValidator<ProductHistoryListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "date",
        "-date"
    };

    public ProductHistoryListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200);

        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: date, -date.");
    }
}
