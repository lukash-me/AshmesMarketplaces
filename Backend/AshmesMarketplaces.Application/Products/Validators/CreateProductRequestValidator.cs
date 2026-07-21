using AshmesMarketplaces.Application.Products.Dtos;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.Shared;
using FluentValidation;

namespace AshmesMarketplaces.Application.Products.Validators;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
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

        RuleFor(x => x.ImageUrls)
            .NotNull();

        RuleForEach(x => x.ImageUrls)
            .NotEmpty()
            .MaximumLength(Constants.URL_MAX_LENGTH)
            .Must(BeHttpUrl)
            .WithMessage("ImageUrls must contain valid absolute HTTP or HTTPS URLs.");

        RuleFor(x => x.VideoUrls)
            .NotNull()
            .Must(urls => urls.Count <= Constants.PRODUCT_VIDEOS_MAX_COUNT)
            .WithMessage($"VideoUrls must contain at most {Constants.PRODUCT_VIDEOS_MAX_COUNT} URLs.");

        RuleForEach(x => x.VideoUrls)
            .NotEmpty()
            .MaximumLength(Constants.URL_MAX_LENGTH)
            .Must(BeHttpUrl)
            .WithMessage("VideoUrls must contain valid absolute HTTP or HTTPS URLs.");
    }

    private static bool BeHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
