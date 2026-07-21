using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public interface IParserInstanceConfigurationService
{
    Task<ServiceResult<IReadOnlyList<ParserInstanceConfigurationDto>>> GetAdminListAsync(CancellationToken cancellationToken);

    Task<ServiceResult<ParserInstanceConfigurationDto>> CreateAsync(
        CreateParserInstanceConfigurationRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserInstanceConfigurationDto>> UpdateAsync(
        Guid id,
        UpdateParserInstanceConfigurationRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
