using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.UserWorkspaces.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.UserWorkspaces.Services;

public sealed class UserWorkspaceService : IUserWorkspaceService
{
    private readonly ApplicationDbContext _dbContext;

    public UserWorkspaceService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<UserWorkspaceListItemResponse>>> GetListAsync(UserWorkspaceListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var memberships = _dbContext.UserWorkspaces.AsNoTracking();

        if (query.IdUser.HasValue)
            memberships = memberships.Where(x => x.IdUser == query.IdUser.Value);

        if (query.IdWorkspace.HasValue)
            memberships = memberships.Where(x => x.IdWorkspace == query.IdWorkspace.Value);

        if (query.IdRole.HasValue)
            memberships = memberships.Where(x => x.IdRole == query.IdRole.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            memberships = memberships.Where(x =>
                _dbContext.Users.Any(u =>
                    u.Id == x.IdUser
                    && (EF.Functions.ILike(u.Login, $"%{search}%")
                        || (u.Email != null && EF.Functions.ILike(u.Email, $"%{search}%"))))
                || _dbContext.Workspaces.Any(w =>
                    w.Id == x.IdWorkspace
                    && EF.Functions.ILike(w.Name, $"%{search}%")));
        }

        memberships = query.Sort?.Trim() switch
        {
            "idUser" => memberships.OrderBy(x => x.IdUser).ThenBy(x => x.IdWorkspace),
            "-idUser" => memberships.OrderByDescending(x => x.IdUser).ThenBy(x => x.IdWorkspace),
            "idWorkspace" => memberships.OrderBy(x => x.IdWorkspace).ThenBy(x => x.IdUser),
            "-idWorkspace" => memberships.OrderByDescending(x => x.IdWorkspace).ThenBy(x => x.IdUser),
            _ => memberships.OrderBy(x => x.IdUser).ThenBy(x => x.IdWorkspace)
        };

        var totalCount = await memberships.CountAsync(cancellationToken);
        var items = await memberships
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UserWorkspaceListItemResponse(
                x.IdUser,
                x.IdWorkspace,
                x.IdRole))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<UserWorkspaceListItemResponse>>.Success(new PagedResponse<UserWorkspaceListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<UserWorkspaceResponse>> GetByIdAsync(Guid idUser, Guid idWorkspace, CancellationToken cancellationToken)
    {
        if (idUser == Guid.Empty)
            return ServiceResult<UserWorkspaceResponse>.BadRequest("User id is required.");

        if (idWorkspace == Guid.Empty)
            return ServiceResult<UserWorkspaceResponse>.BadRequest("Workspace id is required.");

        var membership = await _dbContext.UserWorkspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdUser == idUser && x.IdWorkspace == idWorkspace, cancellationToken);

        return membership is null
            ? ServiceResult<UserWorkspaceResponse>.NotFound("User workspace membership was not found.")
            : ServiceResult<UserWorkspaceResponse>.Success(MapToResponse(membership));
    }

    public async Task<ServiceResult<UserWorkspaceResponse>> CreateAsync(CreateUserWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var referenceCheck = await ValidateReferencesAsync(request.IdUser, request.IdWorkspace, request.IdRole, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<UserWorkspaceResponse>.Conflict(referenceCheck);

        try
        {
            var membership = new UserWorkspace(
                request.IdUser,
                request.IdWorkspace,
                request.IdRole);

            _dbContext.UserWorkspaces.Add(membership);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<UserWorkspaceResponse>.Success(MapToResponse(membership));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<UserWorkspaceResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<UserWorkspaceResponse>.Conflict("User workspace membership cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<UserWorkspaceResponse>> UpdateAsync(
        Guid idUser,
        Guid idWorkspace,
        UpdateUserWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        if (idUser == Guid.Empty)
            return ServiceResult<UserWorkspaceResponse>.BadRequest("User id is required.");

        if (idWorkspace == Guid.Empty)
            return ServiceResult<UserWorkspaceResponse>.BadRequest("Workspace id is required.");

        var membership = await _dbContext.UserWorkspaces
            .FirstOrDefaultAsync(x => x.IdUser == idUser && x.IdWorkspace == idWorkspace, cancellationToken);

        if (membership is null)
            return ServiceResult<UserWorkspaceResponse>.NotFound("User workspace membership was not found.");

        if (!await RoleExistsAsync(request.IdRole, cancellationToken))
            return ServiceResult<UserWorkspaceResponse>.Conflict("Role was not found.");

        try
        {
            _ = new UserWorkspace(idUser, idWorkspace, request.IdRole);

            var entry = _dbContext.Entry(membership);
            entry.Property(x => x.IdRole).CurrentValue = request.IdRole;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<UserWorkspaceResponse>.Success(MapToResponse(membership));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<UserWorkspaceResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<UserWorkspaceResponse>.Conflict("User workspace membership cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid idUser, Guid idWorkspace, CancellationToken cancellationToken)
    {
        if (idUser == Guid.Empty)
            return ServiceResult.BadRequest("User id is required.");

        if (idWorkspace == Guid.Empty)
            return ServiceResult.BadRequest("Workspace id is required.");

        var membership = await _dbContext.UserWorkspaces
            .FirstOrDefaultAsync(x => x.IdUser == idUser && x.IdWorkspace == idWorkspace, cancellationToken);

        if (membership is null)
            return ServiceResult.NotFound("User workspace membership was not found.");

        _dbContext.UserWorkspaces.Remove(membership);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("User workspace membership cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<string?> ValidateReferencesAsync(Guid idUser, Guid idWorkspace, Guid idRole, CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.Users.AsNoTracking().AnyAsync(x => x.Id == idUser, cancellationToken);
        if (!userExists)
            return "User was not found.";

        var workspaceExists = await _dbContext.Workspaces.AsNoTracking().AnyAsync(x => x.Id == idWorkspace, cancellationToken);
        if (!workspaceExists)
            return "Workspace was not found.";

        if (!await RoleExistsAsync(idRole, cancellationToken))
            return "Role was not found.";

        return null;
    }

    private async Task<bool> RoleExistsAsync(Guid idRole, CancellationToken cancellationToken)
    {
        return await _dbContext.Roles.AsNoTracking().AnyAsync(x => x.Id == idRole, cancellationToken);
    }

    private static UserWorkspaceResponse MapToResponse(UserWorkspace membership)
    {
        return new UserWorkspaceResponse(
            membership.IdUser,
            membership.IdWorkspace,
            membership.IdRole);
    }
}
