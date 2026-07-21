using AshmesMarketplaces.Application.Permissions.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Permissions.Validators;

public sealed class PermissionListQueryValidator : AbstractValidator<PermissionListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "-name",
        "domain",
        "-domain",
        "dateCreate",
        "-dateCreate",
        "dateUpdate",
        "-dateUpdate"
    };

    public PermissionListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: name, -name, domain, -domain, dateCreate, -dateCreate, dateUpdate, -dateUpdate.");
        RuleFor(x => x.IdCategory).NotEqual(Guid.Empty).When(x => x.IdCategory.HasValue);
        RuleFor(x => x.Domain).GreaterThanOrEqualTo(0).When(x => x.Domain.HasValue);
    }
}
