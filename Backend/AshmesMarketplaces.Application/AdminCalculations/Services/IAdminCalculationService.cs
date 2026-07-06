using AshmesMarketplaces.Application.AdminCalculations.Dtos;
using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.AdminCalculations.Services;

public interface IAdminCalculationService
{
    Task<ServiceResult<IReadOnlyList<AdminCalculationDto>>> GetCalculationsAsync(CancellationToken cancellationToken);

    Task<ServiceResult<AdminCalculationManualRunDto>> RequestManualRunAsync(
        string scheduleKey,
        CancellationToken cancellationToken);
}
