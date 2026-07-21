using AshmesMarketplaces.Application.CampaignMetrics.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.CampaignMetrics.Validators;

public sealed class CreateCampaignMetricRequestValidator : AbstractValidator<CreateCampaignMetricRequest>
{
    public CreateCampaignMetricRequestValidator()
    {
        RuleFor(x => x.IdCampaign).NotEmpty();
        RuleFor(x => x.ImpressionAmount).GreaterThanOrEqualTo(0).When(x => x.ImpressionAmount.HasValue);
        RuleFor(x => x.ClicksAmount).GreaterThanOrEqualTo(0).When(x => x.ClicksAmount.HasValue);
        RuleFor(x => x.CostDay).GreaterThanOrEqualTo(0).When(x => x.CostDay.HasValue);
        RuleFor(x => x.Date).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("Date must be UTC.");
    }
}
