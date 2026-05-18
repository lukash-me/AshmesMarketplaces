using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Permissions.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Permissions.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _dbContext;

    public PermissionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<PermissionListItemResponse>>> GetListAsync(PermissionListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var permissions = _dbContext.Permissions.AsNoTracking();

        if (query.IdCategory.HasValue)
            permissions = permissions.Where(x => x.IdCategory == query.IdCategory.Value);

        if (query.Domain.HasValue)
            permissions = permissions.Where(x => x.Domain == query.Domain.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            permissions = permissions.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || EF.Functions.ILike(x.Description, $"%{search}%"));
        }

        permissions = query.Sort?.Trim() switch
        {
            "name" => permissions.OrderBy(x => x.Name),
            "-name" => permissions.OrderByDescending(x => x.Name),
            "domain" => permissions.OrderBy(x => x.Domain),
            "-domain" => permissions.OrderByDescending(x => x.Domain),
            "dateCreate" => permissions.OrderBy(x => x.DateCreate),
            "-dateCreate" => permissions.OrderByDescending(x => x.DateCreate),
            "dateUpdate" => permissions.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => permissions.OrderByDescending(x => x.DateUpdate),
            _ => permissions.OrderBy(x => x.Name)
        };

        var totalCount = await permissions.CountAsync(cancellationToken);
        var items = await permissions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PermissionListItemResponse(
                x.Id,
                x.IdCategory,
                x.Name,
                x.Description,
                x.Domain,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<PermissionListItemResponse>>.Success(new PagedResponse<PermissionListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<PermissionResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<PermissionResponse>.BadRequest("Permission id is required.");

        var permission = await _dbContext.Permissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return permission is null
            ? ServiceResult<PermissionResponse>.NotFound("Permission was not found.")
            : ServiceResult<PermissionResponse>.Success(MapToResponse(permission));
    }

    public async Task<ServiceResult<PermissionResponse>> CreateAsync(CreatePermissionRequest request, CancellationToken cancellationToken)
    {
        if (!await PermissionCategoryExistsAsync(request.IdCategory, cancellationToken))
            return ServiceResult<PermissionResponse>.Conflict("Permission category was not found.");

        try
        {
            var permission = new Permission(
                request.IdCategory,
                request.Name,
                request.Description,
                request.Domain,
                request.DateCreate,
                request.DateUpdate);

            _dbContext.Permissions.Add(permission);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<PermissionResponse>.Success(MapToResponse(permission));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<PermissionResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<PermissionResponse>.Conflict("Permission cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<PermissionResponse>> UpdateAsync(Guid id, UpdatePermissionRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<PermissionResponse>.BadRequest("Permission id is required.");

        var permission = await _dbContext.Permissions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (permission is null)
            return ServiceResult<PermissionResponse>.NotFound("Permission was not found.");

        if (!await PermissionCategoryExistsAsync(request.IdCategory, cancellationToken))
            return ServiceResult<PermissionResponse>.Conflict("Permission category was not found.");

        try
        {
            _ = new Permission(
                request.IdCategory,
                request.Name,
                request.Description,
                request.Domain,
                request.DateCreate,
                request.DateUpdate);

            var entry = _dbContext.Entry(permission);
            entry.Property(x => x.IdCategory).CurrentValue = request.IdCategory;
            entry.Property(x => x.Name).CurrentValue = request.Name;
            entry.Property(x => x.Description).CurrentValue = request.Description;
            entry.Property(x => x.Domain).CurrentValue = request.Domain;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<PermissionResponse>.Success(MapToResponse(permission));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<PermissionResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<PermissionResponse>.Conflict("Permission cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Permission id is required.");

        var permission = await _dbContext.Permissions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (permission is null)
            return ServiceResult.NotFound("Permission was not found.");

        _dbContext.Permissions.Remove(permission);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Permission cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<bool> PermissionCategoryExistsAsync(Guid idCategory, CancellationToken cancellationToken)
    {
        return await _dbContext.PermissionCategories.AsNoTracking().AnyAsync(x => x.Id == idCategory, cancellationToken);
    }

    private static PermissionResponse MapToResponse(Permission permission)
    {
        return new PermissionResponse(
            permission.Id,
            permission.IdCategory,
            permission.Name,
            permission.Description,
            permission.Domain,
            permission.DateCreate,
            permission.DateUpdate);
    }
}
