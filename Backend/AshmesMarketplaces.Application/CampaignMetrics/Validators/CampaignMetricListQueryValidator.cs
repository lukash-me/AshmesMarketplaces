using AshmesMarketplaces.Application.CampaignMetrics.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.CampaignMetrics.Validators;

public sealed class CampaignMetricListQueryValidator : AbstractValidator<CampaignMetricListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "date",
        "-date",
        "impressionAmount",
        "-impressionAmount",
        "clicksAmount",
        "-clicksAmount",
        "costDay",
        "-costDay"
    };

    public CampaignMetricListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: date, -date, impressionAmount, -impressionAmount, clicksAmount, -clicksAmount, costDay, -costDay.");
        RuleFor(x => x.IdCampaign).NotEqual(Guid.Empty).When(x => x.IdCampaign.HasValue);
        RuleFor(x => x.DateFrom).Must(BeUtc).WithMessage("DateFrom must be UTC.");
        RuleFor(x => x.DateTo).Must(BeUtc).WithMessage("DateTo must be UTC.");
        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("DateTo cannot be earlier than DateFrom.");
    }

    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
