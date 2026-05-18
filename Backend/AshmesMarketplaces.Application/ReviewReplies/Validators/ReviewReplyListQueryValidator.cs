using AshmesMarketplaces.Application.ReviewReplies.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.ReviewReplies.Validators;

public sealed class ReviewReplyListQueryValidator : AbstractValidator<ReviewReplyListQuery>
{
    private static readonly HashSet<string> AllowedSortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "dateCreate",
        "-dateCreate",
        "dateUpdate",
        "-dateUpdate"
    };

    public ReviewReplyListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort)
            .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSortValues.Contains(sort))
            .WithMessage("Sort must be one of: dateCreate, -dateCreate, dateUpdate, -dateUpdate.");
        RuleFor(x => x.IdReview).NotEqual(Guid.Empty).When(x => x.IdReview.HasValue);
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0).When(x => x.Status.HasValue);
        RuleFor(x => x.DateCreateFrom).Must(BeUtc).WithMessage("DateCreateFrom must be UTC.");
        RuleFor(x => x.DateCreateTo).Must(BeUtc).WithMessage("DateCreateTo must be UTC.");
        RuleFor(x => x.DateCreateTo)
            .GreaterThanOrEqualTo(x => x.DateCreateFrom)
            .When(x => x.DateCreateFrom.HasValue && x.DateCreateTo.HasValue)
            .WithMessage("DateCreateTo cannot be earlier than DateCreateFrom.");
    }

    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
