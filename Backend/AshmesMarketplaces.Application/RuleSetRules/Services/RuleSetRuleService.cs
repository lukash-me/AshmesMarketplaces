using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RuleSetRules.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Rules;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.RuleSetRules.Services;

public sealed class RuleSetRuleService : IRuleSetRuleService
{
    private readonly ApplicationDbContext _dbContext;

    public RuleSetRuleService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<RuleSetRuleListItemResponse>>> GetListAsync(RuleSetRuleListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var ruleSetRules = _dbContext.RuleSetRules.AsNoTracking();

        if (query.IdSet.HasValue)
            ruleSetRules = ruleSetRules.Where(x => x.IdSet == query.IdSet.Value);

        if (query.IdRule.HasValue)
            ruleSetRules = ruleSetRules.Where(x => x.IdRule == query.IdRule.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            ruleSetRules = ruleSetRules.Where(x =>
                _dbContext.RuleSets.Any(s => s.Id == x.IdSet && EF.Functions.ILike(s.Name, $"%{search}%"))
                || _dbContext.Rules.Any(r =>
                    r.Id == x.IdRule
                    && (EF.Functions.ILike(r.Name, $"%{search}%") || EF.Functions.ILike(r.Code, $"%{search}%"))));
        }

        ruleSetRules = query.Sort?.Trim() switch
        {
            "idSet" => ruleSetRules.OrderBy(x => x.IdSet).ThenBy(x => x.IdRule),
            "-idSet" => ruleSetRules.OrderByDescending(x => x.IdSet).ThenBy(x => x.IdRule),
            "idRule" => ruleSetRules.OrderBy(x => x.IdRule).ThenBy(x => x.IdSet),
            "-idRule" => ruleSetRules.OrderByDescending(x => x.IdRule).ThenBy(x => x.IdSet),
            _ => ruleSetRules.OrderBy(x => x.IdSet).ThenBy(x => x.IdRule)
        };

        var totalCount = await ruleSetRules.CountAsync(cancellationToken);
        var items = await ruleSetRules
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RuleSetRuleListItemResponse(
                x.IdSet,
                x.IdRule))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<RuleSetRuleListItemResponse>>.Success(new PagedResponse<RuleSetRuleListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<RuleSetRuleResponse>> GetByIdAsync(Guid idSet, Guid idRule, CancellationToken cancellationToken)
    {
        if (idSet == Guid.Empty)
            return ServiceResult<RuleSetRuleResponse>.BadRequest("Rule set id is required.");

        if (idRule == Guid.Empty)
            return ServiceResult<RuleSetRuleResponse>.BadRequest("Rule id is required.");

        var ruleSetRule = await _dbContext.RuleSetRules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdSet == idSet && x.IdRule == idRule, cancellationToken);

        return ruleSetRule is null
            ? ServiceResult<RuleSetRuleResponse>.NotFound("Rule set rule was not found.")
            : ServiceResult<RuleSetRuleResponse>.Success(MapToResponse(ruleSetRule));
    }

    public async Task<ServiceResult<RuleSetRuleResponse>> CreateAsync(CreateRuleSetRuleRequest request, CancellationToken cancellationToken)
    {
        var referenceCheck = await ValidateReferencesAsync(request.IdSet, request.IdRule, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<RuleSetRuleResponse>.Conflict(referenceCheck);

        try
        {
            var ruleSetRule = new RuleSetRule(request.IdSet, request.IdRule);

            _dbContext.RuleSetRules.Add(ruleSetRule);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RuleSetRuleResponse>.Success(MapToResponse(ruleSetRule));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RuleSetRuleResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RuleSetRuleResponse>.Conflict("Rule set rule cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid idSet, Guid idRule, CancellationToken cancellationToken)
    {
        if (idSet == Guid.Empty)
            return ServiceResult.BadRequest("Rule set id is required.");

        if (idRule == Guid.Empty)
            return ServiceResult.BadRequest("Rule id is required.");

        var ruleSetRule = await _dbContext.RuleSetRules
            .FirstOrDefaultAsync(x => x.IdSet == idSet && x.IdRule == idRule, cancellationToken);

        if (ruleSetRule is null)
            return ServiceResult.NotFound("Rule set rule was not found.");

        _dbContext.RuleSetRules.Remove(ruleSetRule);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Rule set rule cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<string?> ValidateReferencesAsync(Guid idSet, Guid idRule, CancellationToken cancellationToken)
    {
        var ruleSetExists = await _dbContext.RuleSets.AsNoTracking().AnyAsync(x => x.Id == idSet, cancellationToken);
        if (!ruleSetExists)
            return "Rule set was not found.";

        var ruleExists = await _dbContext.Rules.AsNoTracking().AnyAsync(x => x.Id == idRule, cancellationToken);
        if (!ruleExists)
            return "Rule was not found.";

        return null;
    }

    private static RuleSetRuleResponse MapToResponse(RuleSetRule ruleSetRule)
    {
        return new RuleSetRuleResponse(
            ruleSetRule.IdSet,
            ruleSetRule.IdRule);
    }
}
