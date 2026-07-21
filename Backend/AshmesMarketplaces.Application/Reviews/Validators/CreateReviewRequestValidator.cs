using AshmesMarketplaces.Application.Reviews.Dtos;
using AshmesMarketplaces.Domain.Shared;
using FluentValidation;

namespace AshmesMarketplaces.Application.Reviews.Validators;

public sealed class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.IdProduct).NotEmpty();
        RuleFor(x => x.IdOnMp).NotEmpty().MaximumLength(Constants.EXTERNAL_ID_MAX_LENGTH);
        RuleFor(x => x.Rating).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DateCreate).Must(BeUtc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateReply).Must(BeUtc).WithMessage("DateReply must be UTC.");
        RuleFor(x => x.DateReply)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .When(x => x.DateReply.HasValue)
            .WithMessage("DateReply cannot be earlier than DateCreate.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
