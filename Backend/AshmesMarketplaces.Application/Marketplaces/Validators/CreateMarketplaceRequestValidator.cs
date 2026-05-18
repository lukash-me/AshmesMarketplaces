using AshmesMarketplaces.Domain.Shared;
using AshmesMarketplaces.Application.Marketplaces.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Marketplaces.Validators;

public sealed class CreateMarketplaceRequestValidator : AbstractValidator<CreateMarketplaceRequest>
{
    private const int NameMaxLength = 255;
    private const int ShortTextMaxLength = 128;
    private const int CurrencyMaxLength = 32;

    public CreateMarketplaceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(NameMaxLength);

        RuleFor(x => x.ApiUrl)
            .NotEmpty()
            .MaximumLength(Constants.URL_MAX_LENGTH)
            .Must(BeHttpUrl)
            .WithMessage("ApiUrl must be a valid absolute HTTP or HTTPS URL.");

        RuleFor(x => x.ApiVersion)
            .NotEmpty()
            .MaximumLength(ShortTextMaxLength);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .MaximumLength(CurrencyMaxLength);

        RuleFor(x => x.Region)
            .NotEmpty()
            .MaximumLength(ShortTextMaxLength);

        RuleFor(x => x.TypeCommission)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.SchemeDelivery)
            .NotEmpty()
            .MaximumLength(ShortTextMaxLength);

        RuleFor(x => x.DateUpdate)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage("DateUpdate must be UTC.");
    }

    private static bool BeHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
