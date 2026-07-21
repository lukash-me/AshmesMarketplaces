using AshmesMarketplaces.Application.Auth.Dtos;
using AshmesMarketplaces.Domain.Shared;
using FluentValidation;

namespace AshmesMarketplaces.Application.Auth.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Login).NotEmpty().MaximumLength(Constants.LOGIN_MAX_LENGTH);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(Constants.PASSWORD_MAX_LENGTH);
    }
}
