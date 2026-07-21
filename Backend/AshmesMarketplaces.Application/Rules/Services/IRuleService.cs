using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Rules.Dtos;

namespace AshmesMarketplaces.Application.Rules.Services;

public interface IRuleService
{
    Task<ServiceResult<PagedResponse<RuleListItemResponse>>> GetListAsync(RuleListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<RuleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<RuleResponse>> CreateAsync(CreateRuleRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<RuleResponse>> UpdateAsync(Guid id, UpdateRuleRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
