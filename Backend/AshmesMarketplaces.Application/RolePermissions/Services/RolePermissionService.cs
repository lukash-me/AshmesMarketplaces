using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RolePermissions.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.RolePermissions.Services;

public sealed class RolePermissionService : IRolePermissionService
{
    private readonly ApplicationDbContext _dbContext;

    public RolePermissionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<RolePermissionListItemResponse>>> GetListAsync(RolePermissionListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rolePermissions = _dbContext.RolePermissions.AsNoTracking();

        if (query.IdRole.HasValue)
            rolePermissions = rolePermissions.Where(x => x.IdRole == query.IdRole.Value);

        if (query.IdPermission.HasValue)
            rolePermissions = rolePermissions.Where(x => x.IdPermission == query.IdPermission.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rolePermissions = rolePermissions.Where(x =>
                _dbContext.Roles.Any(r => r.Id == x.IdRole && EF.Functions.ILike(r.Name, $"%{search}%"))
                || _dbContext.Permissions.Any(p => p.Id == x.IdPermission && EF.Functions.ILike(p.Name, $"%{search}%")));
        }

        rolePermissions = query.Sort?.Trim() switch
        {
            "idRole" => rolePermissions.OrderBy(x => x.IdRole).ThenBy(x => x.IdPermission),
            "-idRole" => rolePermissions.OrderByDescending(x => x.IdRole).ThenBy(x => x.IdPermission),
            "idPermission" => rolePermissions.OrderBy(x => x.IdPermission).ThenBy(x => x.IdRole),
            "-idPermission" => rolePermissions.OrderByDescending(x => x.IdPermission).ThenBy(x => x.IdRole),
            _ => rolePermissions.OrderBy(x => x.IdRole).ThenBy(x => x.IdPermission)
        };

        var totalCount = await rolePermissions.CountAsync(cancellationToken);
        var items = await rolePermissions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RolePermissionListItemResponse(
                x.IdRole,
                x.IdPermission))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<RolePermissionListItemResponse>>.Success(new PagedResponse<RolePermissionListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<RolePermissionResponse>> GetByIdAsync(Guid idRole, Guid idPermission, CancellationToken cancellationToken)
    {
        if (idRole == Guid.Empty)
            return ServiceResult<RolePermissionResponse>.BadRequest("Role id is required.");

        if (idPermission == Guid.Empty)
            return ServiceResult<RolePermissionResponse>.BadRequest("Permission id is required.");

        var rolePermission = await _dbContext.RolePermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdRole == idRole && x.IdPermission == idPermission, cancellationToken);

        return rolePermission is null
            ? ServiceResult<RolePermissionResponse>.NotFound("Role permission was not found.")
            : ServiceResult<RolePermissionResponse>.Success(MapToResponse(rolePermission));
    }

    public async Task<ServiceResult<RolePermissionResponse>> CreateAsync(CreateRolePermissionRequest request, CancellationToken cancellationToken)
    {
        var referenceCheck = await ValidateReferencesAsync(request.IdRole, request.IdPermission, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<RolePermissionResponse>.Conflict(referenceCheck);

        try
        {
            var rolePermission = new RolePermission(request.IdRole, request.IdPermission);

            _dbContext.RolePermissions.Add(rolePermission);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RolePermissionResponse>.Success(MapToResponse(rolePermission));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RolePermissionResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RolePermissionResponse>.Conflict("Role permission cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid idRole, Guid idPermission, CancellationToken cancellationToken)
    {
        if (idRole == Guid.Empty)
            return ServiceResult.BadRequest("Role id is required.");

        if (idPermission == Guid.Empty)
            return ServiceResult.BadRequest("Permission id is required.");

        var rolePermission = await _dbContext.RolePermissions
            .FirstOrDefaultAsync(x => x.IdRole == idRole && x.IdPermission == idPermission, cancellationToken);

        if (rolePermission is null)
            return ServiceResult.NotFound("Role permission was not found.");

        _dbContext.RolePermissions.Remove(rolePermission);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Role permission cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<string?> ValidateReferencesAsync(Guid idRole, Guid idPermission, CancellationToken cancellationToken)
    {
        var roleExists = await _dbContext.Roles.AsNoTracking().AnyAsync(x => x.Id == idRole, cancellationToken);
        if (!roleExists)
            return "Role was not found.";

        var permissionExists = await _dbContext.Permissions.AsNoTracking().AnyAsync(x => x.Id == idPermission, cancellationToken);
        if (!permissionExists)
            return "Permission was not found.";

        return null;
    }

    private static RolePermissionResponse MapToResponse(RolePermission rolePermission)
    {
        return new RolePermissionResponse(
            rolePermission.IdRole,
            rolePermission.IdPermission);
    }
}
