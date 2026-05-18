using AshmesMarketplaces.Application.Warehouses.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Warehouses.Validators;

public sealed class CreateWarehouseRequestValidator : AbstractValidator<CreateWarehouseRequest>
{
    private const int NameMaxLength = 255;
    private const int ShortTextMaxLength = 128;
    private const int AddressMaxLength = 512;
    private const int CoordinateMaxLength = 64;

    public CreateWarehouseRequestValidator()
    {
        RuleFor(x => x.IdMp)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(NameMaxLength);

        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(ShortTextMaxLength);

        RuleFor(x => x.Region)
            .NotEmpty()
            .MaximumLength(ShortTextMaxLength);

        RuleFor(x => x.City)
            .MaximumLength(ShortTextMaxLength);

        RuleFor(x => x.Address)
            .MaximumLength(AddressMaxLength);

        RuleFor(x => x.Latitude)
            .MaximumLength(CoordinateMaxLength);

        RuleFor(x => x.Longitude)
            .MaximumLength(CoordinateMaxLength);

        RuleFor(x => x.Type)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.DateCreate)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage("DateCreate must be UTC.");

        RuleFor(x => x.DateUpdate)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage("DateUpdate must be UTC.");
    }
}
