using AshmesMarketplaces.Application.Reviews.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Reviews.Validators;

public sealed class ReviewListQueryValidator : AbstractValidator<ReviewListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "dateCreate",
        "-dateCreate",
        "rating",
        "-rating"
    };

    public ReviewListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: dateCreate, -dateCreate, rating, -rating.");
        RuleFor(x => x.IdProduct).NotEqual(Guid.Empty).When(x => x.IdProduct.HasValue);
        RuleFor(x => x.Rating).GreaterThanOrEqualTo(0).When(x => x.Rating.HasValue);
        RuleFor(x => x.DateCreateFrom).Must(BeUtc).WithMessage("DateCreateFrom must be UTC.");
        RuleFor(x => x.DateCreateTo).Must(BeUtc).WithMessage("DateCreateTo must be UTC.");
        RuleFor(x => x.DateCreateTo)
            .GreaterThanOrEqualTo(x => x.DateCreateFrom)
            .When(x => x.DateCreateFrom.HasValue && x.DateCreateTo.HasValue)
            .WithMessage("DateCreateTo cannot be earlier than DateCreateFrom.");
    }

    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
