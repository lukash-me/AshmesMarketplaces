using AshmesMarketplaces.Application.Common.Pagination;
using FluentValidation;

namespace AshmesMarketplaces.Application.Marketplaces.Validators;

public sealed class MarketplaceListQueryValidator : AbstractValidator<ListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "-name",
        "dateUpdate",
        "-dateUpdate"
    };

    public MarketplaceListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200);

        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: name, -name, dateUpdate, -dateUpdate.");
    }
}
