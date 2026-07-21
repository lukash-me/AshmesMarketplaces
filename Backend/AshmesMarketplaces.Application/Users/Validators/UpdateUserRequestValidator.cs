using AshmesMarketplaces.Application.Users.Dtos;
using AshmesMarketplaces.Domain.Shared;
using FluentValidation;

namespace AshmesMarketplaces.Application.Users.Validators;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.IdRole).NotEmpty();
        RuleFor(x => x.Login).NotEmpty().MaximumLength(Constants.LOGIN_MAX_LENGTH);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(Constants.PASSWORD_MAX_LENGTH);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(Constants.EMAIL_MAX_LENGTH).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(Constants.PHONE_MAX_LENGTH);
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DateCreate).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateLogin).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("DateLogin must be UTC.");
        RuleFor(x => x.DateLogin)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .WithMessage("DateLogin cannot be earlier than DateCreate.");
    }
}
