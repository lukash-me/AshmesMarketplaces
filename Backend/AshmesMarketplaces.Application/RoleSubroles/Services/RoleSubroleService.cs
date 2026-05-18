using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RoleSubroles.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.RoleSubroles.Services;

public sealed class RoleSubroleService : IRoleSubroleService
{
    private readonly ApplicationDbContext _dbContext;

    public RoleSubroleService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<RoleSubroleListItemResponse>>> GetListAsync(RoleSubroleListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var roleSubroles = _dbContext.RoleSubroles.AsNoTracking();

        if (query.IdRole.HasValue)
            roleSubroles = roleSubroles.Where(x => x.IdRole == query.IdRole.Value);

        if (query.IdSubrole.HasValue)
            roleSubroles = roleSubroles.Where(x => x.IdSubrole == query.IdSubrole.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            roleSubroles = roleSubroles.Where(x =>
                _dbContext.Roles.Any(r => r.Id == x.IdRole && EF.Functions.ILike(r.Name, $"%{search}%"))
                || _dbContext.Roles.Any(r => r.Id == x.IdSubrole && EF.Functions.ILike(r.Name, $"%{search}%")));
        }

        roleSubroles = query.Sort?.Trim() switch
        {
            "idRole" => roleSubroles.OrderBy(x => x.IdRole).ThenBy(x => x.IdSubrole),
            "-idRole" => roleSubroles.OrderByDescending(x => x.IdRole).ThenBy(x => x.IdSubrole),
            "idSubrole" => roleSubroles.OrderBy(x => x.IdSubrole).ThenBy(x => x.IdRole),
            "-idSubrole" => roleSubroles.OrderByDescending(x => x.IdSubrole).ThenBy(x => x.IdRole),
            _ => roleSubroles.OrderBy(x => x.IdRole).ThenBy(x => x.IdSubrole)
        };

        var totalCount = await roleSubroles.CountAsync(cancellationToken);
        var items = await roleSubroles
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RoleSubroleListItemResponse(
                x.IdRole,
                x.IdSubrole))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<RoleSubroleListItemResponse>>.Success(new PagedResponse<RoleSubroleListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<RoleSubroleResponse>> GetByIdAsync(Guid idRole, Guid idSubrole, CancellationToken cancellationToken)
    {
        if (idRole == Guid.Empty)
            return ServiceResult<RoleSubroleResponse>.BadRequest("Role id is required.");

        if (idSubrole == Guid.Empty)
            return ServiceResult<RoleSubroleResponse>.BadRequest("Subrole id is required.");

        var roleSubrole = await _dbContext.RoleSubroles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdRole == idRole && x.IdSubrole == idSubrole, cancellationToken);

        return roleSubrole is null
            ? ServiceResult<RoleSubroleResponse>.NotFound("Role subrole was not found.")
            : ServiceResult<RoleSubroleResponse>.Success(MapToResponse(roleSubrole));
    }

    public async Task<ServiceResult<RoleSubroleResponse>> CreateAsync(CreateRoleSubroleRequest request, CancellationToken cancellationToken)
    {
        var referenceCheck = await ValidateReferencesAsync(request.IdRole, request.IdSubrole, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<RoleSubroleResponse>.Conflict(referenceCheck);

        try
        {
            var roleSubrole = new RoleSubrole(request.IdRole, request.IdSubrole);

            _dbContext.RoleSubroles.Add(roleSubrole);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RoleSubroleResponse>.Success(MapToResponse(roleSubrole));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RoleSubroleResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RoleSubroleResponse>.Conflict("Role subrole cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid idRole, Guid idSubrole, CancellationToken cancellationToken)
    {
        if (idRole == Guid.Empty)
            return ServiceResult.BadRequest("Role id is required.");

        if (idSubrole == Guid.Empty)
            return ServiceResult.BadRequest("Subrole id is required.");

        var roleSubrole = await _dbContext.RoleSubroles
            .FirstOrDefaultAsync(x => x.IdRole == idRole && x.IdSubrole == idSubrole, cancellationToken);

        if (roleSubrole is null)
            return ServiceResult.NotFound("Role subrole was not found.");

        _dbContext.RoleSubroles.Remove(roleSubrole);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Role subrole cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<string?> ValidateReferencesAsync(Guid idRole, Guid idSubrole, CancellationToken cancellationToken)
    {
        if (idRole == idSubrole)
            return "Role and subrole must be different.";

        var roleExists = await _dbContext.Roles.AsNoTracking().AnyAsync(x => x.Id == idRole, cancellationToken);
        if (!roleExists)
            return "Role was not found.";

        var subroleExists = await _dbContext.Roles.AsNoTracking().AnyAsync(x => x.Id == idSubrole, cancellationToken);
        if (!subroleExists)
            return "Subrole was not found.";

        return null;
    }

    private static RoleSubroleResponse MapToResponse(RoleSubrole roleSubrole)
    {
        return new RoleSubroleResponse(
            roleSubrole.IdRole,
            roleSubrole.IdSubrole);
    }
}
