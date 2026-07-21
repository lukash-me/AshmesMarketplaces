using AshmesMarketplaces.Application.Workspaces.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Workspaces.Validators;

public sealed class WorkspaceListQueryValidator : AbstractValidator<WorkspaceListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "-name",
        "dateCreate",
        "-dateCreate",
        "dateUpdate",
        "-dateUpdate"
    };

    public WorkspaceListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: name, -name, dateCreate, -dateCreate, dateUpdate, -dateUpdate.");
        RuleFor(x => x.IdBrand).NotEqual(Guid.Empty).When(x => x.IdBrand.HasValue);
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0).When(x => x.Status.HasValue);
    }
}
