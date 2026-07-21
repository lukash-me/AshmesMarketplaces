using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RuleConstructor.Dtos;

namespace AshmesMarketplaces.Application.RuleConstructor.Services;

public interface IRuleConstructorService
{
    Task<ServiceResult<IReadOnlyList<RuleConstructorFilterDto>>> GetFiltersAsync(CancellationToken cancellationToken);

    Task<ServiceResult<RuleConstructorSearchResponse>> SearchAsync(
        RuleConstructorSearchRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<RuleConstructorCountsResponse>> GetCountsAsync(
        RuleConstructorCountsRequest request,
        CancellationToken cancellationToken);
}
