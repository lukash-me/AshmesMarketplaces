using AshmesMarketplaces.Application.RuleSetRules.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RuleSetRules.Validators;

public sealed class CreateRuleSetRuleRequestValidator : AbstractValidator<CreateRuleSetRuleRequest>
{
    public CreateRuleSetRuleRequestValidator()
    {
        RuleFor(x => x.IdSet).NotEmpty();
        RuleFor(x => x.IdRule).NotEmpty();
    }
}
