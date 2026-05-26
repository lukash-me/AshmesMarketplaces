using AshmesMarketplaces.Application.ParserObservability.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.ParserObservability.Validators;

public sealed class ParserObservedMarketEventQueryValidator : AbstractValidator<ParserObservedMarketEventQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "eventType",
        "-eventType",
        "quantityChange",
        "-quantityChange",
        "currentQuantity",
        "-currentQuantity",
        "previousQuantity",
        "-previousQuantity",
        "observedAtUtc",
        "-observedAtUtc"
    };

    public ParserObservedMarketEventQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.MinQuantityChange).GreaterThanOrEqualTo(1).When(x => x.MinQuantityChange.HasValue);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort is not allowed for observed market events.");
        RuleFor(x => x.EventType)
            .Must(eventType =>
                string.IsNullOrWhiteSpace(eventType)
                || ParserObservedMarketEventTypes.All.Contains(eventType.Trim()))
            .WithMessage("Event type is not allowed for observed market events.");
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
        RuleFor(x => x)
            .Must(x =>
                string.IsNullOrWhiteSpace(x.CurrentLogisticsRunId)
                || !string.Equals(
                    x.CurrentLogisticsRunId.Trim(),
                    x.PreviousLogisticsRunId?.Trim(),
                    StringComparison.Ordinal))
            .WithMessage("Current and previous logistics run ids must be different.");
    }
}
