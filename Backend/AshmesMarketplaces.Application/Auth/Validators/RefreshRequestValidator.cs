using AshmesMarketplaces.Application.Auth.Dtos;
using AshmesMarketplaces.Domain.Shared;
using FluentValidation;

namespace AshmesMarketplaces.Application.Auth.Validators;

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.SessionId).GreaterThan(0);
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(Constants.TOKEN_MAX_LENGTH);
    }
}
