using AshmesMarketplaces.Application.Campaigns.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Campaigns.Validators;

public sealed class CampaignListQueryValidator : AbstractValidator<CampaignListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "-name",
        "dateCreate",
        "-dateCreate",
        "dateUpdate",
        "-dateUpdate",
        "dateStart",
        "-dateStart",
        "budget",
        "-budget"
    };

    public CampaignListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: name, -name, dateCreate, -dateCreate, dateUpdate, -dateUpdate, dateStart, -dateStart, budget, -budget.");
        RuleFor(x => x.IdProduct).NotEqual(Guid.Empty).When(x => x.IdProduct.HasValue);
        RuleFor(x => x.IdSetCampaign).NotEqual(Guid.Empty).When(x => x.IdSetCampaign.HasValue);
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0).When(x => x.Status.HasValue);
        RuleFor(x => x.Type).GreaterThanOrEqualTo(0).When(x => x.Type.HasValue);
        RuleFor(x => x.DateStartFrom).Must(BeUtc).WithMessage("DateStartFrom must be UTC.");
        RuleFor(x => x.DateStartTo).Must(BeUtc).WithMessage("DateStartTo must be UTC.");
        RuleFor(x => x.DateEndFrom).Must(BeUtc).WithMessage("DateEndFrom must be UTC.");
        RuleFor(x => x.DateEndTo).Must(BeUtc).WithMessage("DateEndTo must be UTC.");
        RuleFor(x => x.DateStartTo)
            .GreaterThanOrEqualTo(x => x.DateStartFrom)
            .When(x => x.DateStartFrom.HasValue && x.DateStartTo.HasValue)
            .WithMessage("DateStartTo cannot be earlier than DateStartFrom.");
        RuleFor(x => x.DateEndTo)
            .GreaterThanOrEqualTo(x => x.DateEndFrom)
            .When(x => x.DateEndFrom.HasValue && x.DateEndTo.HasValue)
            .WithMessage("DateEndTo cannot be earlier than DateEndFrom.");
    }

    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
