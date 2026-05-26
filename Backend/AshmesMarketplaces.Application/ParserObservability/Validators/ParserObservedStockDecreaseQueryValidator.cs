using AshmesMarketplaces.Application.ParserObservability.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.ParserObservability.Validators;

public sealed class ParserObservedStockDecreaseQueryValidator : AbstractValidator<ParserObservedStockDecreaseQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "decrease",
        "-decrease",
        "currentQuantity",
        "-currentQuantity",
        "previousQuantity",
        "-previousQuantity",
        "observedAtUtc",
        "-observedAtUtc"
    };

    public ParserObservedStockDecreaseQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.MinDecrease).GreaterThanOrEqualTo(1).When(x => x.MinDecrease.HasValue);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort is not allowed for observed stock decreases.");
        RuleFor(x => x.Search).MaximumLength(512);
        RuleFor(x => x.SourceCategory).MaximumLength(255);
        RuleFor(x => x.SourceSubcategory).MaximumLength(255);
        RuleFor(x => x.BrandName).MaximumLength(255);
        RuleFor(x => x.SellerName).MaximumLength(512);
        RuleFor(x => x.CurrentLogisticsRunId).MaximumLength(128);
        RuleFor(x => x.PreviousLogisticsRunId).MaximumLength(128);
        RuleFor(x => x)
            .Must(x =>
                string.IsNullOrWhiteSpace(x.CurrentLogisticsRunId) == string.IsNullOrWhiteSpace(x.PreviousLogisticsRunId))
            .WithMessage("Current and previous logistics run ids must be provided together.");
    }
}
