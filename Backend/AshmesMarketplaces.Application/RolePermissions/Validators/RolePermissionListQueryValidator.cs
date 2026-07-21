using AshmesMarketplaces.Application.RolePermissions.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RolePermissions.Validators;

public sealed class RolePermissionListQueryValidator : AbstractValidator<RolePermissionListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "idRole",
        "-idRole",
        "idPermission",
        "-idPermission"
    };

    public RolePermissionListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: idRole, -idRole, idPermission, -idPermission.");
        RuleFor(x => x.IdRole).NotEqual(Guid.Empty).When(x => x.IdRole.HasValue);
        RuleFor(x => x.IdPermission).NotEqual(Guid.Empty).When(x => x.IdPermission.HasValue);
    }
}
