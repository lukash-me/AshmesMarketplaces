using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RuleSets.Dtos;

namespace AshmesMarketplaces.Application.RuleSets.Services;

public interface IRuleSetService
{
    Task<ServiceResult<PagedResponse<RuleSetListItemResponse>>> GetListAsync(RuleSetListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<RuleSetResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<RuleSetResponse>> CreateAsync(CreateRuleSetRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<RuleSetResponse>> UpdateAsync(Guid id, UpdateRuleSetRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
