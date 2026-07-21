using AshmesMarketplaces.Application.Logistics.Dtos;
using FluentValidation;

namespace AshmesMarketplaces.Application.Logistics.Validators;

public sealed class UpdateLogisticRequestValidator : AbstractValidator<UpdateLogisticRequest>
{
    public UpdateLogisticRequestValidator()
    {
        RuleFor(x => x.IdProduct).NotEmpty();
        RuleFor(x => x.IdWarehouse).NotEqual(Guid.Empty).When(x => x.IdWarehouse.HasValue);
        RuleFor(x => x.StockAmount).GreaterThanOrEqualTo(0).When(x => x.StockAmount.HasValue);
        RuleFor(x => x.StockAmountStatistic).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockInTransit).GreaterThanOrEqualTo(0).When(x => x.StockInTransit.HasValue);
        RuleFor(x => x.CostStorage).GreaterThanOrEqualTo(0).When(x => x.CostStorage.HasValue);
        RuleFor(x => x.CostLogistic).GreaterThanOrEqualTo(0).When(x => x.CostLogistic.HasValue);
        RuleFor(x => x.Type).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Date).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("Date must be UTC.");
    }
}
