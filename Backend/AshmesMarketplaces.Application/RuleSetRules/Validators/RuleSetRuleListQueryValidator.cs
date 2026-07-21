using AshmesMarketplaces.Application.RuleSetRules.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.RuleSetRules.Validators;

public sealed class RuleSetRuleListQueryValidator : AbstractValidator<RuleSetRuleListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "idSet",
        "-idSet",
        "idRule",
        "-idRule"
    };

    public RuleSetRuleListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: idSet, -idSet, idRule, -idRule.");
        RuleFor(x => x.IdSet).NotEqual(Guid.Empty).When(x => x.IdSet.HasValue);
        RuleFor(x => x.IdRule).NotEqual(Guid.Empty).When(x => x.IdRule.HasValue);
    }
}
