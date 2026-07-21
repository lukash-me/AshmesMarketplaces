using AshmesMarketplaces.Application.Products.Dtos;
using AshmesMarketplaces.Domain.Shared;
using FluentValidation;

namespace AshmesMarketplaces.Application.Products.Validators;

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.IdMp)
            .NotEmpty();

        RuleFor(x => x.IdBrand)
            .NotEqual(Guid.Empty)
            .When(x => x.IdBrand.HasValue);

        RuleFor(x => x.IdCategory)
            .NotEqual(Guid.Empty)
            .When(x => x.IdCategory.HasValue);

        RuleFor(x => x.IdOnMp)
            .MaximumLength(Constants.EXTERNAL_ID_MAX_LENGTH);

        RuleFor(x => x.SkuProduct)
            .MaximumLength(Constants.EXTERNAL_ID_MAX_LENGTH);

        RuleFor(x => x.SkuSeller)
            .NotEmpty()
            .MaximumLength(Constants.EXTERNAL_ID_MAX_LENGTH);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Constants.PRODUCT_NAME_MAX_LENGTH);

        RuleFor(x => x.Description)
            .MaximumLength(Constants.PRODUCT_DESCRIPTION_MAX_LENGTH);

        RuleFor(x => x.Barcode)
            .MaximumLength(Constants.BARCODE_MAX_LENGTH)
            .Matches("^[0-9]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.Barcode))
            .WithMessage("Barcode must contain only digits.");

        RuleFor(x => x.Commission)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Commission.HasValue);

        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.DateUpdated)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage("DateUpdated must be UTC.");

        RuleFor(x => x.DateCreated)
            .Must(date => date is null || date.Value.Kind == DateTimeKind.Utc)
            .WithMessage("DateCreated must be UTC.");
    }
}
