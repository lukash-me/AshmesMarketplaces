using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RuleSetRules.Dtos;

namespace AshmesMarketplaces.Application.RuleSetRules.Services;

public interface IRuleSetRuleService
{
    Task<ServiceResult<PagedResponse<RuleSetRuleListItemResponse>>> GetListAsync(RuleSetRuleListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<RuleSetRuleResponse>> GetByIdAsync(Guid idSet, Guid idRule, CancellationToken cancellationToken);
    Task<ServiceResult<RuleSetRuleResponse>> CreateAsync(CreateRuleSetRuleRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid idSet, Guid idRule, CancellationToken cancellationToken);
}
