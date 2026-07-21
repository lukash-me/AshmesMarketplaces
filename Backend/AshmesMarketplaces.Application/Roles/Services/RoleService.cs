using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Roles.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Roles.Services;

public sealed class RoleService : IRoleService
{
    private readonly ApplicationDbContext _dbContext;

    public RoleService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<RoleListItemResponse>>> GetListAsync(RoleListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var roles = _dbContext.Roles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            roles = roles.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || (x.Description != null && EF.Functions.ILike(x.Description, $"%{search}%")));
        }

        roles = query.Sort?.Trim() switch
        {
            "name" => roles.OrderBy(x => x.Name),
            "-name" => roles.OrderByDescending(x => x.Name),
            "dateCreate" => roles.OrderBy(x => x.DateCreate),
            "-dateCreate" => roles.OrderByDescending(x => x.DateCreate),
            "dateUpdate" => roles.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => roles.OrderByDescending(x => x.DateUpdate),
            _ => roles.OrderBy(x => x.Name)
        };

        var totalCount = await roles.CountAsync(cancellationToken);
        var items = await roles
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RoleListItemResponse(
                x.Id,
                x.Name,
                x.Description,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<RoleListItemResponse>>.Success(new PagedResponse<RoleListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<RoleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<RoleResponse>.BadRequest("Role id is required.");

        var role = await _dbContext.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return role is null
            ? ServiceResult<RoleResponse>.NotFound("Role was not found.")
            : ServiceResult<RoleResponse>.Success(MapToResponse(role));
    }

    public async Task<ServiceResult<RoleResponse>> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var role = new Role(
                request.Name,
                request.Description,
                request.DateCreate,
                request.DateUpdate);

            _dbContext.Roles.Add(role);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RoleResponse>.Success(MapToResponse(role));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RoleResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RoleResponse>.Conflict("Role cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<RoleResponse>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<RoleResponse>.BadRequest("Role id is required.");

        var role = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (role is null)
            return ServiceResult<RoleResponse>.NotFound("Role was not found.");

        try
        {
            _ = new Role(
                request.Name,
                request.Description,
                request.DateCreate,
                request.DateUpdate);

            var entry = _dbContext.Entry(role);
            entry.Property(x => x.Name).CurrentValue = request.Name;
            entry.Property(x => x.Description).CurrentValue = request.Description;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RoleResponse>.Success(MapToResponse(role));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RoleResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RoleResponse>.Conflict("Role cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Role id is required.");

        var role = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (role is null)
            return ServiceResult.NotFound("Role was not found.");

        _dbContext.Roles.Remove(role);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Role cannot be deleted because it is referenced by other records.");
        }
    }

    private static RoleResponse MapToResponse(Role role)
    {
        return new RoleResponse(
            role.Id,
            role.Name,
            role.Description,
            role.DateCreate,
            role.DateUpdate);
    }
}
