using AshmesMarketplaces.Application.ParserObservability.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.ParserObservability.Validators;

public sealed class ParserProductListQueryValidator : AbstractValidator<ParserProductListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "-name",
        "parsedAtUtc",
        "-parsedAtUtc",
        "wbProductId",
        "-wbProductId",
        "priceDiscounted",
        "-priceDiscounted",
        "reviewRating",
        "-reviewRating",
        "feedbackCount",
        "-feedbackCount"
    };

    public ParserProductListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort is not allowed for parser products.");
        RuleFor(x => x.PriceDiscountedFrom)
            .LessThanOrEqualTo(x => x.PriceDiscountedTo)
            .When(x => x.PriceDiscountedFrom.HasValue && x.PriceDiscountedTo.HasValue);
        RuleFor(x => x.ReviewRatingFrom)
            .LessThanOrEqualTo(x => x.ReviewRatingTo)
            .When(x => x.ReviewRatingFrom.HasValue && x.ReviewRatingTo.HasValue);
        RuleFor(x => x.FeedbackCountFrom)
            .LessThanOrEqualTo(x => x.FeedbackCountTo)
            .When(x => x.FeedbackCountFrom.HasValue && x.FeedbackCountTo.HasValue);
    }
}
