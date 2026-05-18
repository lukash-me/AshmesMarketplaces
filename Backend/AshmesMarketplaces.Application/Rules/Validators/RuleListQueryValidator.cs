using AshmesMarketplaces.Application.Rules.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Rules.Validators;

public sealed class RuleListQueryValidator : AbstractValidator<RuleListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "-name",
        "code",
        "-code",
        "number",
        "-number",
        "domain",
        "-domain",
        "dateCreate",
        "-dateCreate",
        "dateUpdate",
        "-dateUpdate"
    };

    public RuleListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: name, -name, code, -code, number, -number, domain, -domain, dateCreate, -dateCreate, dateUpdate, -dateUpdate.");
        RuleFor(x => x.Number).GreaterThanOrEqualTo(0).When(x => x.Number.HasValue);
        RuleFor(x => x.Domain).GreaterThanOrEqualTo(0).When(x => x.Domain.HasValue);
    }
}
