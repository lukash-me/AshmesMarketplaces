using AshmesMarketplaces.Application.Users.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Users.Validators;

public sealed class UserListQueryValidator : AbstractValidator<UserListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "login",
        "-login",
        "dateCreate",
        "-dateCreate",
        "dateLogin",
        "-dateLogin"
    };

    public UserListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: login, -login, dateCreate, -dateCreate, dateLogin, -dateLogin.");
        RuleFor(x => x.IdRole).NotEqual(Guid.Empty).When(x => x.IdRole.HasValue);
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0).When(x => x.Status.HasValue);
    }
}
