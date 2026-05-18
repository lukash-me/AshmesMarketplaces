using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RuleSets.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Rules;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.RuleSets.Services;

public sealed class RuleSetService : IRuleSetService
{
    private readonly ApplicationDbContext _dbContext;

    public RuleSetService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<RuleSetListItemResponse>>> GetListAsync(RuleSetListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var ruleSets = _dbContext.RuleSets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            ruleSets = ruleSets.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || (x.Description != null && EF.Functions.ILike(x.Description, $"%{search}%")));
        }

        ruleSets = query.Sort?.Trim() switch
        {
            "name" => ruleSets.OrderBy(x => x.Name),
            "-name" => ruleSets.OrderByDescending(x => x.Name),
            "dateCreate" => ruleSets.OrderBy(x => x.DateCreate),
            "-dateCreate" => ruleSets.OrderByDescending(x => x.DateCreate),
            "dateUpdate" => ruleSets.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => ruleSets.OrderByDescending(x => x.DateUpdate),
            _ => ruleSets.OrderBy(x => x.Name)
        };

        var totalCount = await ruleSets.CountAsync(cancellationToken);
        var items = await ruleSets
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RuleSetListItemResponse(
                x.Id,
                x.Name,
                x.Description,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<RuleSetListItemResponse>>.Success(new PagedResponse<RuleSetListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<RuleSetResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<RuleSetResponse>.BadRequest("Rule set id is required.");

        var ruleSet = await _dbContext.RuleSets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return ruleSet is null
            ? ServiceResult<RuleSetResponse>.NotFound("Rule set was not found.")
            : ServiceResult<RuleSetResponse>.Success(MapToResponse(ruleSet));
    }

    public async Task<ServiceResult<RuleSetResponse>> CreateAsync(CreateRuleSetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var ruleSet = new RuleSet(
                request.Name,
                request.Description,
                request.DateUpdate,
                request.DateCreate);

            _dbContext.RuleSets.Add(ruleSet);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RuleSetResponse>.Success(MapToResponse(ruleSet));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RuleSetResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RuleSetResponse>.Conflict("Rule set cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<RuleSetResponse>> UpdateAsync(Guid id, UpdateRuleSetRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<RuleSetResponse>.BadRequest("Rule set id is required.");

        var ruleSet = await _dbContext.RuleSets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ruleSet is null)
            return ServiceResult<RuleSetResponse>.NotFound("Rule set was not found.");

        try
        {
            _ = new RuleSet(
                request.Name,
                request.Description,
                request.DateUpdate,
                request.DateCreate);

            var entry = _dbContext.Entry(ruleSet);
            entry.Property(x => x.Name).CurrentValue = request.Name;
            entry.Property(x => x.Description).CurrentValue = request.Description;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RuleSetResponse>.Success(MapToResponse(ruleSet));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RuleSetResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RuleSetResponse>.Conflict("Rule set cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Rule set id is required.");

        var ruleSet = await _dbContext.RuleSets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ruleSet is null)
            return ServiceResult.NotFound("Rule set was not found.");

        _dbContext.RuleSets.Remove(ruleSet);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Rule set cannot be deleted because it is referenced by other records.");
        }
    }

    private static RuleSetResponse MapToResponse(RuleSet ruleSet)
    {
        return new RuleSetResponse(
            ruleSet.Id,
            ruleSet.Name,
            ruleSet.Description,
            ruleSet.DateCreate,
            ruleSet.DateUpdate);
    }
}
