using AshmesMarketplaces.Application.PermissionCategories.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.PermissionCategories.Validators;

public sealed class PermissionCategoryListQueryValidator : AbstractValidator<PermissionCategoryListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "-name",
        "dateCreate",
        "-dateCreate",
        "dateUpdate",
        "-dateUpdate"
    };

    public PermissionCategoryListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: name, -name, dateCreate, -dateCreate, dateUpdate, -dateUpdate.");
    }
}
