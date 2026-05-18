using AshmesMarketplaces.Application.RoleSubroles.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RoleSubroles.Validators;

public sealed class CreateRoleSubroleRequestValidator : AbstractValidator<CreateRoleSubroleRequest>
{
    public CreateRoleSubroleRequestValidator()
    {
        RuleFor(x => x.IdRole).NotEmpty();
        RuleFor(x => x.IdSubrole).NotEmpty();
        RuleFor(x => x.IdSubrole)
            .NotEqual(x => x.IdRole)
            .WithMessage("Role and subrole must be different.");
    }
}
