using AshmesMarketplaces.Application.Campaigns.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Campaigns.Validators;

public sealed class UpdateCampaignRequestValidator : AbstractValidator<UpdateCampaignRequest>
{
    private const int NameMaxLength = 255;
    private const int RegionMaxLength = 128;

    public UpdateCampaignRequestValidator()
    {
        RuleFor(x => x.IdProduct).NotEmpty();
        RuleFor(x => x.IdSetCampaign).NotEqual(Guid.Empty).When(x => x.IdSetCampaign.HasValue);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0).When(x => x.Budget.HasValue);
        RuleFor(x => x.Region).MaximumLength(RegionMaxLength);
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Type).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DateStart).Must(BeUtc).WithMessage("DateStart must be UTC.");
        RuleFor(x => x.DateEnd).Must(BeUtc).WithMessage("DateEnd must be UTC.");
        RuleFor(x => x.DateCreate).Must(BeUtc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateUpdate).Must(BeUtc).WithMessage("DateUpdate must be UTC.");
        RuleFor(x => x.DateEnd)
            .GreaterThanOrEqualTo(x => x.DateStart)
            .When(x => x.DateStart.HasValue && x.DateEnd.HasValue)
            .WithMessage("DateEnd cannot be earlier than DateStart.");
        RuleFor(x => x.DateUpdate)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .WithMessage("DateUpdate cannot be earlier than DateCreate.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
