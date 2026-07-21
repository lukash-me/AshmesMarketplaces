using AshmesMarketplaces.Application.Orders.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Orders.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    private const int LocationMaxLength = 255;

    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.IdProduct).NotEmpty();
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.LocationSource).MaximumLength(LocationMaxLength);
        RuleFor(x => x.LocationDestination).MaximumLength(LocationMaxLength);
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DateOpened).Must(BeUtc).WithMessage("DateOpened must be UTC.");
        RuleFor(x => x.DateUpdate).Must(BeUtc).WithMessage("DateUpdate must be UTC.");
        RuleFor(x => x.DateDelivered).Must(BeUtc).WithMessage("DateDelivered must be UTC.");
        RuleFor(x => x.DateClosed).Must(BeUtc).WithMessage("DateClosed must be UTC.");
        RuleFor(x => x.DateDelivered)
            .GreaterThanOrEqualTo(x => x.DateOpened)
            .When(x => x.DateDelivered.HasValue)
            .WithMessage("DateDelivered cannot be earlier than DateOpened.");
        RuleFor(x => x.DateClosed)
            .GreaterThanOrEqualTo(x => x.DateOpened)
            .When(x => x.DateClosed.HasValue)
            .WithMessage("DateClosed cannot be earlier than DateOpened.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
    private static bool BeUtc(DateTime? value) => value is null || value.Value.Kind == DateTimeKind.Utc;
}
