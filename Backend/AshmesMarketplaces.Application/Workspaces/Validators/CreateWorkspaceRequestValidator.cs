using AshmesMarketplaces.Application.Workspaces.Dtos;
using AshmesMarketplaces.Domain.Shared;
using FluentValidation;

namespace AshmesMarketplaces.Application.Workspaces.Validators;

public sealed class CreateWorkspaceRequestValidator : AbstractValidator<CreateWorkspaceRequest>
{
    private const int NameMaxLength = 255;

    public CreateWorkspaceRequestValidator()
    {
        RuleFor(x => x.IdBrand).NotEqual(Guid.Empty).When(x => x.IdBrand.HasValue);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(x => x.UrlInvite)
            .MaximumLength(Constants.URL_MAX_LENGTH)
            .Matches(Constants.LINK_REGEX)
            .When(x => !string.IsNullOrWhiteSpace(x.UrlInvite))
            .WithMessage("UrlInvite must be a valid URL.");
        RuleFor(x => x.Status).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DateCreate).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("DateCreate must be UTC.");
        RuleFor(x => x.DateUpdate).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("DateUpdate must be UTC.");
        RuleFor(x => x.DateUpdate)
            .GreaterThanOrEqualTo(x => x.DateCreate)
            .WithMessage("DateUpdate cannot be earlier than DateCreate.");
    }
}
