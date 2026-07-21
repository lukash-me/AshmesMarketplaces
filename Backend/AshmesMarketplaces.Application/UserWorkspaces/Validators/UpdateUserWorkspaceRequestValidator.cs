using AshmesMarketplaces.Application.UserWorkspaces.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.UserWorkspaces.Validators;

public sealed class UpdateUserWorkspaceRequestValidator : AbstractValidator<UpdateUserWorkspaceRequest>
{
    public UpdateUserWorkspaceRequestValidator()
    {
        RuleFor(x => x.IdRole).NotEmpty();
    }
}
