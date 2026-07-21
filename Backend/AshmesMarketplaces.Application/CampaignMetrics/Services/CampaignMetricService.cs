using AshmesMarketplaces.Application.CampaignMetrics.Dtos;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Advertising;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.CampaignMetrics.Services;

public sealed class CampaignMetricService : ICampaignMetricService
{
    private readonly ApplicationDbContext _dbContext;

    public CampaignMetricService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<CampaignMetricListItemResponse>>> GetListAsync(CampaignMetricListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var metrics = _dbContext.CampaignMetrics.AsNoTracking();

        if (query.IdCampaign.HasValue)
            metrics = metrics.Where(x => x.IdCampaign == query.IdCampaign.Value);

        if (query.DateFrom.HasValue)
            metrics = metrics.Where(x => x.Date >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            metrics = metrics.Where(x => x.Date <= query.DateTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            metrics = metrics.Where(x => _dbContext.Campaigns.Any(c =>
                c.Id == x.IdCampaign
                && EF.Functions.ILike(c.Name, $"%{search}%")));
        }

        metrics = query.Sort?.Trim() switch
        {
            "date" => metrics.OrderBy(x => x.Date),
            "-date" => metrics.OrderByDescending(x => x.Date),
            "impressionAmount" => metrics.OrderBy(x => x.ImpressionAmount),
            "-impressionAmount" => metrics.OrderByDescending(x => x.ImpressionAmount),
            "clicksAmount" => metrics.OrderBy(x => x.ClicksAmount),
            "-clicksAmount" => metrics.OrderByDescending(x => x.ClicksAmount),
            "costDay" => metrics.OrderBy(x => x.CostDay),
            "-costDay" => metrics.OrderByDescending(x => x.CostDay),
            _ => metrics.OrderByDescending(x => x.Date)
        };

        var totalCount = await metrics.CountAsync(cancellationToken);
        var items = await metrics
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CampaignMetricListItemResponse(
                x.Id,
                x.IdCampaign,
                x.ImpressionAmount,
                x.ClicksAmount,
                x.CostDay,
                x.Date))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<CampaignMetricListItemResponse>>.Success(new PagedResponse<CampaignMetricListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<CampaignMetricResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<CampaignMetricResponse>.BadRequest("Campaign metric id is required.");

        var metric = await _dbContext.CampaignMetrics.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return metric is null
            ? ServiceResult<CampaignMetricResponse>.NotFound("Campaign metric was not found.")
            : ServiceResult<CampaignMetricResponse>.Success(MapToResponse(metric));
    }

    public async Task<ServiceResult<CampaignMetricResponse>> CreateAsync(CreateCampaignMetricRequest request, CancellationToken cancellationToken)
    {
        if (!await CampaignExistsAsync(request.IdCampaign, cancellationToken))
            return ServiceResult<CampaignMetricResponse>.Conflict("Campaign was not found.");

        try
        {
            var metric = new CampaignMetric(
                request.IdCampaign,
                request.ImpressionAmount,
                request.ClicksAmount,
                request.CostDay,
                request.Date);

            _dbContext.CampaignMetrics.Add(metric);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<CampaignMetricResponse>.Success(MapToResponse(metric));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<CampaignMetricResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<CampaignMetricResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<CampaignMetricResponse>.Conflict("Campaign metric cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<CampaignMetricResponse>> UpdateAsync(Guid id, UpdateCampaignMetricRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<CampaignMetricResponse>.BadRequest("Campaign metric id is required.");

        var metric = await _dbContext.CampaignMetrics.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (metric is null)
            return ServiceResult<CampaignMetricResponse>.NotFound("Campaign metric was not found.");

        if (!await CampaignExistsAsync(request.IdCampaign, cancellationToken))
            return ServiceResult<CampaignMetricResponse>.Conflict("Campaign was not found.");

        try
        {
            _ = new CampaignMetric(
                request.IdCampaign,
                request.ImpressionAmount,
                request.ClicksAmount,
                request.CostDay,
                request.Date);

            var entry = _dbContext.Entry(metric);
            entry.Property(x => x.IdCampaign).CurrentValue = request.IdCampaign;
            entry.Property(x => x.ImpressionAmount).CurrentValue = request.ImpressionAmount;
            entry.Property(x => x.ClicksAmount).CurrentValue = request.ClicksAmount;
            entry.Property(x => x.CostDay).CurrentValue = request.CostDay;
            entry.Property(x => x.Date).CurrentValue = request.Date;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<CampaignMetricResponse>.Success(MapToResponse(metric));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<CampaignMetricResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<CampaignMetricResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<CampaignMetricResponse>.Conflict("Campaign metric cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Campaign metric id is required.");

        var metric = await _dbContext.CampaignMetrics.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (metric is null)
            return ServiceResult.NotFound("Campaign metric was not found.");

        _dbContext.CampaignMetrics.Remove(metric);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Campaign metric cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<bool> CampaignExistsAsync(Guid idCampaign, CancellationToken cancellationToken)
    {
        return await _dbContext.Campaigns.AsNoTracking().AnyAsync(x => x.Id == idCampaign, cancellationToken);
    }

    private static CampaignMetricResponse MapToResponse(CampaignMetric metric)
    {
        return new CampaignMetricResponse(
            metric.Id,
            metric.IdCampaign,
            metric.ImpressionAmount,
            metric.ClicksAmount,
            metric.CostDay,
            metric.Date);
    }
}
