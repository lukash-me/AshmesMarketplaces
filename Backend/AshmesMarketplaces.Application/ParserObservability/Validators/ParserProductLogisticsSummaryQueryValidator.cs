using AshmesMarketplaces.Application.ParserObservability.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.ParserObservability.Validators;

public sealed class ParserProductLogisticsSummaryQueryValidator : AbstractValidator<ParserProductLogisticsSummaryQuery>
{
    public ParserProductLogisticsSummaryQueryValidator()
    {
        RuleFor(x => x.ParserRunId).MaximumLength(128);
        RuleFor(x => x.Search).MaximumLength(512);
        RuleFor(x => x.SourceCategory).MaximumLength(255);
        RuleFor(x => x.SourceSubcategory).MaximumLength(255);
        RuleFor(x => x.BrandName).MaximumLength(255);
        RuleFor(x => x.SellerName).MaximumLength(512);
        RuleFor(x => x.WbRootId).MaximumLength(128);
    }
}
