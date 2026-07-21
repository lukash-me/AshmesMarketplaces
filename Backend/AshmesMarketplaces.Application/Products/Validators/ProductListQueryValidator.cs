using AshmesMarketplaces.Application.Products.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Products.Validators;

public sealed class ProductListQueryValidator : AbstractValidator<ProductListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "-name",
        "dateUpdated",
        "-dateUpdated",
        "dateCreated",
        "-dateCreated",
        "skuSeller",
        "-skuSeller"
    };

    public ProductListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200);

        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: name, -name, dateUpdated, -dateUpdated, dateCreated, -dateCreated, skuSeller, -skuSeller.");

        RuleFor(x => x.IdMp)
            .NotEqual(Guid.Empty)
            .When(x => x.IdMp.HasValue);

        RuleFor(x => x.IdBrand)
            .NotEqual(Guid.Empty)
            .When(x => x.IdBrand.HasValue);

        RuleFor(x => x.IdCategory)
            .NotEqual(Guid.Empty)
            .When(x => x.IdCategory.HasValue);

        RuleFor(x => x.Status)
            .IsInEnum()
            .When(x => x.Status.HasValue);
    }
}
