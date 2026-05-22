using AshmesMarketplaces.Application.ParserObservability.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.ParserObservability.Validators;

public sealed class ParserReviewListQueryValidator : AbstractValidator<ParserReviewListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAtOnMp",
        "-createdAtOnMp",
        "parsedAtUtc",
        "-parsedAtUtc",
        "rating",
        "-rating"
    };

    public ParserReviewListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort is not allowed for parser reviews.");
        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5)
            .When(x => x.Rating.HasValue);
        RuleFor(x => x.CreatedAtOnMpFrom)
            .LessThanOrEqualTo(x => x.CreatedAtOnMpTo)
            .When(x => x.CreatedAtOnMpFrom.HasValue && x.CreatedAtOnMpTo.HasValue);
    }
}
