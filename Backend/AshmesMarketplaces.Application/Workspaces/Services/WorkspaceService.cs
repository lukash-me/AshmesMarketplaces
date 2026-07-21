using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Workspaces.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Workspaces.Services;

public sealed class WorkspaceService : IWorkspaceService
{
    private readonly ApplicationDbContext _dbContext;

    public WorkspaceService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<WorkspaceListItemResponse>>> GetListAsync(WorkspaceListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var workspaces = _dbContext.Workspaces.AsNoTracking();

        if (query.IdBrand.HasValue)
            workspaces = workspaces.Where(x => x.IdBrand == query.IdBrand.Value);

        if (query.Status.HasValue)
            workspaces = workspaces.Where(x => x.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            workspaces = workspaces.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || (x.Description != null && EF.Functions.ILike(x.Description, $"%{search}%"))
                || (x.UrlInvite != null && EF.Functions.ILike(x.UrlInvite, $"%{search}%")));
        }

        workspaces = query.Sort?.Trim() switch
        {
            "name" => workspaces.OrderBy(x => x.Name),
            "-name" => workspaces.OrderByDescending(x => x.Name),
            "dateCreate" => workspaces.OrderBy(x => x.DateCreate),
            "-dateCreate" => workspaces.OrderByDescending(x => x.DateCreate),
            "dateUpdate" => workspaces.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => workspaces.OrderByDescending(x => x.DateUpdate),
            _ => workspaces.OrderBy(x => x.Name)
        };

        var totalCount = await workspaces.CountAsync(cancellationToken);
        var items = await workspaces
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WorkspaceListItemResponse(
                x.Id,
                x.IdBrand,
                x.Name,
                x.Description,
                x.UrlInvite,
                x.Status,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<WorkspaceListItemResponse>>.Success(new PagedResponse<WorkspaceListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<WorkspaceResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<WorkspaceResponse>.BadRequest("Workspace id is required.");

        var workspace = await _dbContext.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return workspace is null
            ? ServiceResult<WorkspaceResponse>.NotFound("Workspace was not found.")
            : ServiceResult<WorkspaceResponse>.Success(MapToResponse(workspace));
    }

    public async Task<ServiceResult<WorkspaceResponse>> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (request.IdBrand.HasValue && !await BrandExistsAsync(request.IdBrand.Value, cancellationToken))
            return ServiceResult<WorkspaceResponse>.Conflict("Brand was not found.");

        try
        {
            var workspace = new Workspace(
                request.IdBrand,
                request.Name,
                request.Description,
                request.UrlInvite,
                request.Status,
                request.DateCreate,
                request.DateUpdate);

            _dbContext.Workspaces.Add(workspace);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<WorkspaceResponse>.Success(MapToResponse(workspace));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<WorkspaceResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<WorkspaceResponse>.Conflict("Workspace cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<WorkspaceResponse>> UpdateAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<WorkspaceResponse>.BadRequest("Workspace id is required.");

        var workspace = await _dbContext.Workspaces.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (workspace is null)
            return ServiceResult<WorkspaceResponse>.NotFound("Workspace was not found.");

        if (request.IdBrand.HasValue && !await BrandExistsAsync(request.IdBrand.Value, cancellationToken))
            return ServiceResult<WorkspaceResponse>.Conflict("Brand was not found.");

        try
        {
            _ = new Workspace(
                request.IdBrand,
                request.Name,
                request.Description,
                request.UrlInvite,
                request.Status,
                request.DateCreate,
                request.DateUpdate);

            var entry = _dbContext.Entry(workspace);
            entry.Property(x => x.IdBrand).CurrentValue = request.IdBrand;
            entry.Property(x => x.Name).CurrentValue = request.Name;
            entry.Property(x => x.Description).CurrentValue = request.Description;
            entry.Property(x => x.UrlInvite).CurrentValue = request.UrlInvite;
            entry.Property(x => x.Status).CurrentValue = request.Status;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<WorkspaceResponse>.Success(MapToResponse(workspace));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<WorkspaceResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<WorkspaceResponse>.Conflict("Workspace cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Workspace id is required.");

        var workspace = await _dbContext.Workspaces.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (workspace is null)
            return ServiceResult.NotFound("Workspace was not found.");

        _dbContext.Workspaces.Remove(workspace);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Workspace cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<bool> BrandExistsAsync(Guid idBrand, CancellationToken cancellationToken)
    {
        return await _dbContext.Brands.AsNoTracking().AnyAsync(x => x.Id == idBrand, cancellationToken);
    }

    private static WorkspaceResponse MapToResponse(Workspace workspace)
    {
        return new WorkspaceResponse(
            workspace.Id,
            workspace.IdBrand,
            workspace.Name,
            workspace.Description,
            workspace.UrlInvite,
            workspace.Status,
            workspace.DateCreate,
            workspace.DateUpdate);
    }
}
