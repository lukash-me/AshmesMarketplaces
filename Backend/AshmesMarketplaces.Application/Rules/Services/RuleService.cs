using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Rules.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Rules;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Rules.Services;

public sealed class RuleService : IRuleService
{
    private readonly ApplicationDbContext _dbContext;

    public RuleService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<RuleListItemResponse>>> GetListAsync(RuleListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rules = _dbContext.Rules.AsNoTracking();

        if (query.Domain.HasValue)
            rules = rules.Where(x => x.Domain == query.Domain.Value);

        if (query.Number.HasValue)
            rules = rules.Where(x => x.Number == query.Number.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rules = rules.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || EF.Functions.ILike(x.Code, $"%{search}%")
                || (x.Description != null && EF.Functions.ILike(x.Description, $"%{search}%")));
        }

        rules = query.Sort?.Trim() switch
        {
            "name" => rules.OrderBy(x => x.Name),
            "-name" => rules.OrderByDescending(x => x.Name),
            "code" => rules.OrderBy(x => x.Code),
            "-code" => rules.OrderByDescending(x => x.Code),
            "number" => rules.OrderBy(x => x.Number),
            "-number" => rules.OrderByDescending(x => x.Number),
            "domain" => rules.OrderBy(x => x.Domain),
            "-domain" => rules.OrderByDescending(x => x.Domain),
            "dateCreate" => rules.OrderBy(x => x.DateCreate),
            "-dateCreate" => rules.OrderByDescending(x => x.DateCreate),
            "dateUpdate" => rules.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => rules.OrderByDescending(x => x.DateUpdate),
            _ => rules.OrderBy(x => x.Name)
        };

        var totalCount = await rules.CountAsync(cancellationToken);
        var items = await rules
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RuleListItemResponse(
                x.Id,
                x.Name,
                x.Description,
                x.Code,
                x.Number,
                x.Domain,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<RuleListItemResponse>>.Success(new PagedResponse<RuleListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<RuleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<RuleResponse>.BadRequest("Rule id is required.");

        var rule = await _dbContext.Rules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return rule is null
            ? ServiceResult<RuleResponse>.NotFound("Rule was not found.")
            : ServiceResult<RuleResponse>.Success(MapToResponse(rule));
    }

    public async Task<ServiceResult<RuleResponse>> CreateAsync(CreateRuleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var rule = new Rule(
                request.Name,
                request.Description,
                request.Code,
                request.Number,
                request.Domain,
                request.DateCreate,
                request.DateUpdate);

            _dbContext.Rules.Add(rule);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RuleResponse>.Success(MapToResponse(rule));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RuleResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RuleResponse>.Conflict("Rule cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<RuleResponse>> UpdateAsync(Guid id, UpdateRuleRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<RuleResponse>.BadRequest("Rule id is required.");

        var rule = await _dbContext.Rules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is null)
            return ServiceResult<RuleResponse>.NotFound("Rule was not found.");

        try
        {
            _ = new Rule(
                request.Name,
                request.Description,
                request.Code,
                request.Number,
                request.Domain,
                request.DateCreate,
                request.DateUpdate);

            var entry = _dbContext.Entry(rule);
            entry.Property(x => x.Name).CurrentValue = request.Name;
            entry.Property(x => x.Description).CurrentValue = request.Description;
            entry.Property(x => x.Code).CurrentValue = request.Code;
            entry.Property(x => x.Number).CurrentValue = request.Number;
            entry.Property(x => x.Domain).CurrentValue = request.Domain;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RuleResponse>.Success(MapToResponse(rule));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RuleResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RuleResponse>.Conflict("Rule cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Rule id is required.");

        var rule = await _dbContext.Rules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is null)
            return ServiceResult.NotFound("Rule was not found.");

        _dbContext.Rules.Remove(rule);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Rule cannot be deleted because it is referenced by other records.");
        }
    }

    private static RuleResponse MapToResponse(Rule rule)
    {
        return new RuleResponse(
            rule.Id,
            rule.Name,
            rule.Description,
            rule.Code,
            rule.Number,
            rule.Domain,
            rule.DateCreate,
            rule.DateUpdate);
    }
}
