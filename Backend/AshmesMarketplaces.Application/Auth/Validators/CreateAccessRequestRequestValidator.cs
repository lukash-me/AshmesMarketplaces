using AshmesMarketplaces.Application.Auth.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Auth.Validators;

public sealed class CreateAccessRequestRequestValidator : AbstractValidator<CreateAccessRequestRequest>
{
    public CreateAccessRequestRequestValidator()
    {
        RuleFor(x => x.Contact).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Comment).MaximumLength(2000);
        RuleFor(x => x.SourcePath).MaximumLength(512);
    }
}
