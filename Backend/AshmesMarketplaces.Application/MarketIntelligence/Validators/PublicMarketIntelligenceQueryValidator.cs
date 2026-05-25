using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.MarketIntelligence.Validators;

public sealed class PublicMarketIntelligenceQueryValidator : AbstractValidator<PublicMarketIntelligenceQuery>
{
    public PublicMarketIntelligenceQueryValidator()
    {
        RuleFor(x => x.TopN).InclusiveBetween(1, 1000);
        RuleFor(x => x.Sort).MaximumLength(64);
        RuleFor(x => x.SourceCategory).MaximumLength(512);
        RuleFor(x => x.SourceSubcategory).MaximumLength(512);
        RuleFor(x => x.Query).MaximumLength(512);
        RuleFor(x => x.SourceRegionDest).MaximumLength(128);
        RuleFor(x => x.LatestRankRunId).MaximumLength(160);
        RuleFor(x => x.BaselineRankRunId).MaximumLength(160);
        RuleFor(x => x.LatestProductRunId).MaximumLength(160);
        RuleFor(x => x.BaselineProductRunId).MaximumLength(160);
    }
}
