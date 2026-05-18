using System.Text.Json;
using AshmesMarketplaces.Application.Campaigns.Dtos;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Advertising;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Campaigns.Services;

public sealed class CampaignService : ICampaignService
{
    private readonly ApplicationDbContext _dbContext;

    public CampaignService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<CampaignListItemResponse>>> GetListAsync(CampaignListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var campaigns = _dbContext.Campaigns.AsNoTracking();

        if (query.IdProduct.HasValue)
            campaigns = campaigns.Where(x => x.IdProduct == ProductId.Create(query.IdProduct.Value));

        if (query.IdSetCampaign.HasValue)
            campaigns = campaigns.Where(x => x.IdSetCampaign == query.IdSetCampaign.Value);

        if (query.Status.HasValue)
            campaigns = campaigns.Where(x => x.Status == query.Status.Value);

        if (query.Type.HasValue)
            campaigns = campaigns.Where(x => x.Type == query.Type.Value);

        if (query.DateStartFrom.HasValue)
            campaigns = campaigns.Where(x => x.DateStart >= query.DateStartFrom.Value);

        if (query.DateStartTo.HasValue)
            campaigns = campaigns.Where(x => x.DateStart <= query.DateStartTo.Value);

        if (query.DateEndFrom.HasValue)
            campaigns = campaigns.Where(x => x.DateEnd >= query.DateEndFrom.Value);

        if (query.DateEndTo.HasValue)
            campaigns = campaigns.Where(x => x.DateEnd <= query.DateEndTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            campaigns = campaigns.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || (x.Region != null && EF.Functions.ILike(x.Region, $"%{search}%"))
                || (x.Description != null && EF.Functions.ILike(x.Description, $"%{search}%")));
        }

        campaigns = query.Sort?.Trim() switch
        {
            "name" => campaigns.OrderBy(x => x.Name),
            "-name" => campaigns.OrderByDescending(x => x.Name),
            "dateCreate" => campaigns.OrderBy(x => x.DateCreate),
            "-dateCreate" => campaigns.OrderByDescending(x => x.DateCreate),
            "dateUpdate" => campaigns.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => campaigns.OrderByDescending(x => x.DateUpdate),
            "dateStart" => campaigns.OrderBy(x => x.DateStart),
            "-dateStart" => campaigns.OrderByDescending(x => x.DateStart),
            "budget" => campaigns.OrderBy(x => x.Budget),
            "-budget" => campaigns.OrderByDescending(x => x.Budget),
            _ => campaigns.OrderByDescending(x => x.DateUpdate)
        };

        var totalCount = await campaigns.CountAsync(cancellationToken);
        var items = await campaigns
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CampaignListItemResponse(
                x.Id,
                x.IdProduct.Value,
                x.IdSetCampaign,
                x.Name,
                x.Budget,
                x.Region,
                x.Status,
                x.Type,
                x.DateStart,
                x.DateEnd,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<CampaignListItemResponse>>.Success(new PagedResponse<CampaignListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<CampaignResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<CampaignResponse>.BadRequest("Campaign id is required.");

        var campaign = await _dbContext.Campaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return campaign is null
            ? ServiceResult<CampaignResponse>.NotFound("Campaign was not found.")
            : ServiceResult<CampaignResponse>.Success(MapToResponse(campaign));
    }

    public async Task<ServiceResult<CampaignResponse>> CreateAsync(CreateCampaignRequest request, CancellationToken cancellationToken)
    {
        var referenceCheck = await ValidateReferencesAsync(request.IdProduct, request.IdSetCampaign, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<CampaignResponse>.Conflict(referenceCheck);

        try
        {
            var campaign = new Campaign(
                ProductId.Create(request.IdProduct),
                request.IdSetCampaign,
                request.Name,
                request.Budget,
                request.Region,
                request.Status,
                request.Type,
                request.Description,
                CloneJsonDocument(request.TimeToImpression),
                request.DateStart,
                request.DateEnd,
                request.DateCreate,
                request.DateUpdate);

            _dbContext.Campaigns.Add(campaign);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<CampaignResponse>.Success(MapToResponse(campaign));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<CampaignResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<CampaignResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<CampaignResponse>.Conflict("Campaign cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<CampaignResponse>> UpdateAsync(Guid id, UpdateCampaignRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<CampaignResponse>.BadRequest("Campaign id is required.");

        var campaign = await _dbContext.Campaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (campaign is null)
            return ServiceResult<CampaignResponse>.NotFound("Campaign was not found.");

        var referenceCheck = await ValidateReferencesAsync(request.IdProduct, request.IdSetCampaign, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<CampaignResponse>.Conflict(referenceCheck);

        try
        {
            _ = new Campaign(
                ProductId.Create(request.IdProduct),
                request.IdSetCampaign,
                request.Name,
                request.Budget,
                request.Region,
                request.Status,
                request.Type,
                request.Description,
                null,
                request.DateStart,
                request.DateEnd,
                request.DateCreate,
                request.DateUpdate);

            var entry = _dbContext.Entry(campaign);
            entry.Property(x => x.IdProduct).CurrentValue = ProductId.Create(request.IdProduct);
            entry.Property(x => x.IdSetCampaign).CurrentValue = request.IdSetCampaign;
            entry.Property(x => x.Name).CurrentValue = request.Name;
            entry.Property(x => x.Budget).CurrentValue = request.Budget;
            entry.Property(x => x.Region).CurrentValue = request.Region;
            entry.Property(x => x.Status).CurrentValue = request.Status;
            entry.Property(x => x.Type).CurrentValue = request.Type;
            entry.Property(x => x.Description).CurrentValue = request.Description;
            entry.Property(x => x.TimeToImpression).CurrentValue = CloneJsonDocument(request.TimeToImpression);
            entry.Property(x => x.DateStart).CurrentValue = request.DateStart;
            entry.Property(x => x.DateEnd).CurrentValue = request.DateEnd;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<CampaignResponse>.Success(MapToResponse(campaign));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<CampaignResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<CampaignResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<CampaignResponse>.Conflict("Campaign cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Campaign id is required.");

        var campaign = await _dbContext.Campaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (campaign is null)
            return ServiceResult.NotFound("Campaign was not found.");

        _dbContext.Campaigns.Remove(campaign);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Campaign cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<string?> ValidateReferencesAsync(Guid idProduct, Guid? idSetCampaign, CancellationToken cancellationToken)
    {
        var productExists = await _dbContext.Products.AsNoTracking().AnyAsync(x => x.Id == ProductId.Create(idProduct), cancellationToken);
        if (!productExists)
            return "Product was not found.";

        if (idSetCampaign.HasValue)
        {
            var ruleSetExists = await _dbContext.RuleSets.AsNoTracking().AnyAsync(x => x.Id == idSetCampaign.Value, cancellationToken);
            if (!ruleSetExists)
                return "Rule set was not found.";
        }

        return null;
    }

    private static CampaignResponse MapToResponse(Campaign campaign)
    {
        return new CampaignResponse(
            campaign.Id,
            campaign.IdProduct.Value,
            campaign.IdSetCampaign,
            campaign.Name,
            campaign.Budget,
            campaign.Region,
            campaign.Status,
            campaign.Type,
            campaign.Description,
            CloneJsonElement(campaign.TimeToImpression),
            campaign.DateStart,
            campaign.DateEnd,
            campaign.DateCreate,
            campaign.DateUpdate);
    }

    private static JsonElement? CloneJsonElement(JsonDocument? document)
    {
        return document is null
            ? null
            : document.RootElement.Clone();
    }

    private static JsonDocument? CloneJsonDocument(JsonElement? element)
    {
        return element.HasValue
            ? JsonDocument.Parse(element.Value.GetRawText())
            : null;
    }
}
