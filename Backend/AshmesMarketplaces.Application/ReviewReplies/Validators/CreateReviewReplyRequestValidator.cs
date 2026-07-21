using AshmesMarketplaces.Application.ReviewReplies.Dtos;
using AshmesMarketplaces.Domain.Shared;
using FluentValidation;

namespace AshmesMarketplaces.Application.ReviewReplies.Validators;

public sealed class CreateReviewReplyRequestValidator : AbstractValidator<CreateReviewReplyRequest>
{
    public CreateReviewReplyRequestValidator()
    {
        RuleFor(x => x.IdReview).NotEmpty();
        RuleFor(x => x.IdOnMp).NotEmpty().MaximumLength(Constants.EXTERNAL_ID_MAX_LENGTH);
        RuleFor(x => x.Text).NotEmpty();
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DateCreate).Must(BeUtc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateUpdate).Must(BeUtc).WithMessage("DateUpdate must be UTC.");
        RuleFor(x => x.DateUpdate)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .WithMessage("DateUpdate cannot be earlier than DateCreate.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
