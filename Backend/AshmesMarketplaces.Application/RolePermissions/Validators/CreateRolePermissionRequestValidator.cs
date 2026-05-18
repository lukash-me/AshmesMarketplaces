using AshmesMarketplaces.Application.RolePermissions.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RolePermissions.Validators;

public sealed class CreateRolePermissionRequestValidator : AbstractValidator<CreateRolePermissionRequest>
{
    public CreateRolePermissionRequestValidator()
    {
        RuleFor(x => x.IdRole).NotEmpty();
        RuleFor(x => x.IdPermission).NotEmpty();
    }
}
