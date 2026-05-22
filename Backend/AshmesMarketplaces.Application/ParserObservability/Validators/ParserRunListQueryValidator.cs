using AshmesMarketplaces.Application.ParserObservability.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.ParserObservability.Validators;

public sealed class ParserRunListQueryValidator : AbstractValidator<ParserRunListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "startedAtUtc",
        "-startedAtUtc",
        "finishedAtUtc",
        "-finishedAtUtc",
        "dateRegisteredUtc",
        "-dateRegisteredUtc"
    };

    public ParserRunListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort is not allowed for parser runs.");
    }
}
