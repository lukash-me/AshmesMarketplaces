using AshmesMarketplaces.Application.UserWorkspaces.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.UserWorkspaces.Validators;

public sealed class UserWorkspaceListQueryValidator : AbstractValidator<UserWorkspaceListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "idUser",
        "-idUser",
        "idWorkspace",
        "-idWorkspace"
    };

    public UserWorkspaceListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: idUser, -idUser, idWorkspace, -idWorkspace.");
        RuleFor(x => x.IdUser).NotEqual(Guid.Empty).When(x => x.IdUser.HasValue);
        RuleFor(x => x.IdWorkspace).NotEqual(Guid.Empty).When(x => x.IdWorkspace.HasValue);
        RuleFor(x => x.IdRole).NotEqual(Guid.Empty).When(x => x.IdRole.HasValue);
    }
}
