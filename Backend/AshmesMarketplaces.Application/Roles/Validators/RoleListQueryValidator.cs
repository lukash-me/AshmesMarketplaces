using AshmesMarketplaces.Application.Roles.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Roles.Validators;

public sealed class RoleListQueryValidator : AbstractValidator<RoleListQuery>
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

    public RoleListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: name, -name, dateCreate, -dateCreate, dateUpdate, -dateUpdate.");
    }
}
