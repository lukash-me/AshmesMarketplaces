using AshmesMarketplaces.Application.UserWorkspaces.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.UserWorkspaces.Validators;

public sealed class CreateUserWorkspaceRequestValidator : AbstractValidator<CreateUserWorkspaceRequest>
{
    public CreateUserWorkspaceRequestValidator()
    {
        RuleFor(x => x.IdUser).NotEmpty();
        RuleFor(x => x.IdWorkspace).NotEmpty();
        RuleFor(x => x.IdRole).NotEmpty();
    }
}
