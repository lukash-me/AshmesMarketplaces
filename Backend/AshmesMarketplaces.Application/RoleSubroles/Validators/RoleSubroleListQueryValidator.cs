using AshmesMarketplaces.Application.RoleSubroles.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RoleSubroles.Validators;

public sealed class RoleSubroleListQueryValidator : AbstractValidator<RoleSubroleListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "idRole",
        "-idRole",
        "idSubrole",
        "-idSubrole"
    };

    public RoleSubroleListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: idRole, -idRole, idSubrole, -idSubrole.");
        RuleFor(x => x.IdRole).NotEqual(Guid.Empty).When(x => x.IdRole.HasValue);
        RuleFor(x => x.IdSubrole).NotEqual(Guid.Empty).When(x => x.IdSubrole.HasValue);
    }
}
