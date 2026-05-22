using AshmesMarketplaces.Application.ParserObservability.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.ParserObservability.Validators;

public sealed class ParserReviewReplyListQueryValidator : AbstractValidator<ParserReviewReplyListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAtOnMp",
        "-createdAtOnMp",
        "updatedAtOnMp",
        "-updatedAtOnMp"
    };

    public ParserReviewReplyListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort is not allowed for parser review replies.");
    }
}
