using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Workspaces.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Finance;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AshmesMarketplaces.Application.Workspaces.Services;

public sealed class ManagementWorkspaceService : IManagementWorkspaceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ManagementWorkspaceService(ApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyCollection<ManagementWorkspaceResponse>>> GetListAsync(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return ServiceResult<IReadOnlyCollection<ManagementWorkspaceResponse>>.Unauthorized("Authentication is required.");

        var workspaceIds = await _dbContext.UserWorkspaces
            .Where(x => x.IdUser == userId.Value)
            .Select(x => x.IdWorkspace)
            .ToListAsync(cancellationToken);

        if (workspaceIds.Count == 0)
            return ServiceResult<IReadOnlyCollection<ManagementWorkspaceResponse>>.Success(Array.Empty<ManagementWorkspaceResponse>());

        var workspaces = await _dbContext.Workspaces
            .Where(x => workspaceIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var members = await LoadMembersAsync(workspaceIds, userId.Value, cancellationToken);
        var response = workspaces
            .Select(workspace => ToResponse(workspace, members.GetValueOrDefault(workspace.Id) ?? Array.Empty<ManagementWorkspaceMemberResponse>()))
            .ToList();

        return ServiceResult<IReadOnlyCollection<ManagementWorkspaceResponse>>.Success(response);
    }

    public async Task<ServiceResult<IReadOnlyCollection<ManagementWorkspaceRoleResponse>>> GetRolesAsync(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return ServiceResult<IReadOnlyCollection<ManagementWorkspaceRoleResponse>>.Unauthorized("Authentication is required.");

        var roles = await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ManagementWorkspaceRoleResponse(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<ManagementWorkspaceRoleResponse>>.Success(roles);
    }

    public async Task<ServiceResult<ManagementWorkspaceResponse>> CreateAsync(
        CreateManagementWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return ServiceResult<ManagementWorkspaceResponse>.Unauthorized("Authentication is required.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult<ManagementWorkspaceResponse>.BadRequest("Workspace name is required.");

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);

        if (user is null)
            return ServiceResult<ManagementWorkspaceResponse>.Unauthorized("Current user was not found.");

        var roleExists = await _dbContext.Roles
            .AnyAsync(x => x.Id == user.IdRole, cancellationToken);

        if (!roleExists)
            return ServiceResult<ManagementWorkspaceResponse>.Conflict("Current user role was not found.");

        var idBrand = await _dbContext.UserWorkspaces
            .Where(x => x.IdUser == userId.Value)
            .Join(
                _dbContext.Workspaces,
                membership => membership.IdWorkspace,
                workspace => workspace.Id,
                (membership, workspace) => workspace)
            .Where(x => x.IdBrand != null)
            .OrderBy(x => x.DateCreate)
            .ThenBy(x => x.Name)
            .Select(x => x.IdBrand)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var workspace = new Workspace(
            idBrand,
            name,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            null,
            1,
            now,
            now);

        _dbContext.Workspaces.Add(workspace);
        _dbContext.UserWorkspaces.Add(new UserWorkspace(user.Id, workspace.Id, user.IdRole));
        await _dbContext.SaveChangesAsync(cancellationToken);

        var members = await LoadMembersAsync(new[] { workspace.Id }, user.Id, cancellationToken);
        return ServiceResult<ManagementWorkspaceResponse>.Success(
            ToResponse(workspace, members.GetValueOrDefault(workspace.Id) ?? Array.Empty<ManagementWorkspaceMemberResponse>()));
    }

    public async Task<ServiceResult<ManagementWorkspaceResponse>> UpdateAsync(
        Guid workspaceId,
        UpdateManagementWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return ServiceResult<ManagementWorkspaceResponse>.Unauthorized("Authentication is required.");

        if (workspaceId == Guid.Empty)
            return ServiceResult<ManagementWorkspaceResponse>.BadRequest("Workspace id is required.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult<ManagementWorkspaceResponse>.BadRequest("Workspace name is required.");

        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(x => x.Id == workspaceId, cancellationToken);

        if (workspace is null)
            return ServiceResult<ManagementWorkspaceResponse>.NotFound("Workspace was not found.");

        var hasAccess = await _dbContext.UserWorkspaces
            .AnyAsync(x => x.IdWorkspace == workspaceId && x.IdUser == userId.Value, cancellationToken);

        if (!hasAccess)
            return ServiceResult<ManagementWorkspaceResponse>.Forbidden("User does not have access to this workspace.");

        var entry = _dbContext.Entry(workspace);
        entry.Property(x => x.Name).CurrentValue = name;
        entry.Property(x => x.Description).CurrentValue = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        entry.Property(x => x.DateUpdate).CurrentValue = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var members = await LoadMembersAsync(new[] { workspace.Id }, userId.Value, cancellationToken);
        return ServiceResult<ManagementWorkspaceResponse>.Success(
            ToResponse(workspace, members.GetValueOrDefault(workspace.Id) ?? Array.Empty<ManagementWorkspaceMemberResponse>()));
    }

    public async Task<ServiceResult<ManagementWorkspaceMemberResponse>> AddMemberAsync(
        Guid workspaceId,
        AddManagementWorkspaceMemberRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return ServiceResult<ManagementWorkspaceMemberResponse>.Unauthorized("Authentication is required.");

        if (workspaceId == Guid.Empty)
            return ServiceResult<ManagementWorkspaceMemberResponse>.BadRequest("Workspace id is required.");

        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return ServiceResult<ManagementWorkspaceMemberResponse>.BadRequest("Email is required.");

        if (request.IdRole == Guid.Empty)
            return ServiceResult<ManagementWorkspaceMemberResponse>.BadRequest("Role is required.");

        var workspaceExists = await _dbContext.Workspaces
            .AnyAsync(x => x.Id == workspaceId, cancellationToken);

        if (!workspaceExists)
            return ServiceResult<ManagementWorkspaceMemberResponse>.NotFound("Workspace was not found.");

        var hasAccess = await _dbContext.UserWorkspaces
            .AnyAsync(x => x.IdWorkspace == workspaceId && x.IdUser == userId.Value, cancellationToken);

        if (!hasAccess)
            return ServiceResult<ManagementWorkspaceMemberResponse>.Forbidden("User does not have access to this workspace.");

        var normalizedEmail = email.ToLower();
        var targetUser = await _dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Login.ToLower() == normalizedEmail
                    || (x.Email != null && x.Email.ToLower() == normalizedEmail),
                cancellationToken);

        if (targetUser is null)
            return ServiceResult<ManagementWorkspaceMemberResponse>.NotFound("User was not found.");

        var alreadyMember = await _dbContext.UserWorkspaces
            .AnyAsync(x => x.IdWorkspace == workspaceId && x.IdUser == targetUser.Id, cancellationToken);

        if (alreadyMember)
            return ServiceResult<ManagementWorkspaceMemberResponse>.Conflict("User already belongs to workspace.");

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(x => x.Id == request.IdRole, cancellationToken);

        if (role is null)
            return ServiceResult<ManagementWorkspaceMemberResponse>.Conflict("Role was not found.");

        _dbContext.UserWorkspaces.Add(new UserWorkspace(targetUser.Id, workspaceId, role.Id));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ManagementWorkspaceMemberResponse>.Success(
            new ManagementWorkspaceMemberResponse(
                targetUser.Id,
                targetUser.Login,
                targetUser.Email,
                role.Id,
                role.Name,
                targetUser.Id == userId.Value));
    }

    public async Task<ServiceResult<ManagementWorkspaceMemberResponse>> UpdateMemberRoleAsync(
        Guid workspaceId,
        Guid userId,
        UpdateManagementWorkspaceMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return ServiceResult<ManagementWorkspaceMemberResponse>.Unauthorized("Authentication is required.");

        if (workspaceId == Guid.Empty)
            return ServiceResult<ManagementWorkspaceMemberResponse>.BadRequest("Workspace id is required.");

        if (userId == Guid.Empty)
            return ServiceResult<ManagementWorkspaceMemberResponse>.BadRequest("User id is required.");

        if (request.IdRole == Guid.Empty)
            return ServiceResult<ManagementWorkspaceMemberResponse>.BadRequest("Role is required.");

        var workspaceExists = await _dbContext.Workspaces
            .AnyAsync(x => x.Id == workspaceId, cancellationToken);

        if (!workspaceExists)
            return ServiceResult<ManagementWorkspaceMemberResponse>.NotFound("Workspace was not found.");

        var hasAccess = await _dbContext.UserWorkspaces
            .AnyAsync(x => x.IdWorkspace == workspaceId && x.IdUser == currentUserId.Value, cancellationToken);

        if (!hasAccess)
            return ServiceResult<ManagementWorkspaceMemberResponse>.Forbidden("User does not have access to this workspace.");

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(x => x.Id == request.IdRole, cancellationToken);

        if (role is null)
            return ServiceResult<ManagementWorkspaceMemberResponse>.Conflict("Role was not found.");

        var membership = await _dbContext.UserWorkspaces
            .FirstOrDefaultAsync(x => x.IdWorkspace == workspaceId && x.IdUser == userId, cancellationToken);

        if (membership is null)
            return ServiceResult<ManagementWorkspaceMemberResponse>.NotFound("User workspace membership was not found.");

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
            return ServiceResult<ManagementWorkspaceMemberResponse>.NotFound("User was not found.");

        _dbContext.Entry(membership).Property(x => x.IdRole).CurrentValue = role.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ManagementWorkspaceMemberResponse>.Success(
            new ManagementWorkspaceMemberResponse(
                user.Id,
                user.Login,
                user.Email,
                role.Id,
                role.Name,
                user.Id == currentUserId.Value));
    }

    public async Task<ServiceResult> DeleteMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return ServiceResult.Unauthorized("Authentication is required.");

        if (workspaceId == Guid.Empty)
            return ServiceResult.BadRequest("Workspace id is required.");

        if (userId == Guid.Empty)
            return ServiceResult.BadRequest("User id is required.");

        var workspaceExists = await _dbContext.Workspaces
            .AnyAsync(x => x.Id == workspaceId, cancellationToken);

        if (!workspaceExists)
            return ServiceResult.NotFound("Workspace was not found.");

        var hasAccess = await _dbContext.UserWorkspaces
            .AnyAsync(x => x.IdWorkspace == workspaceId && x.IdUser == currentUserId.Value, cancellationToken);

        if (!hasAccess)
            return ServiceResult.Forbidden("User does not have access to this workspace.");

        var membership = await _dbContext.UserWorkspaces
            .FirstOrDefaultAsync(x => x.IdWorkspace == workspaceId && x.IdUser == userId, cancellationToken);

        if (membership is null)
            return ServiceResult.NotFound("User workspace membership was not found.");

        _dbContext.UserWorkspaces.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return ServiceResult.Unauthorized("Authentication is required.");

        if (workspaceId == Guid.Empty)
            return ServiceResult.BadRequest("Workspace id is required.");

        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(x => x.Id == workspaceId, cancellationToken);

        if (workspace is null)
            return ServiceResult.NotFound("Workspace was not found.");

        var hasAccess = await _dbContext.UserWorkspaces
            .AnyAsync(x => x.IdWorkspace == workspaceId && x.IdUser == currentUserId.Value, cancellationToken);

        if (!hasAccess)
            return ServiceResult.Forbidden("User does not have access to this workspace.");

        IDbContextTransaction? transaction = null;
        if (_dbContext.Database.IsRelational())
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await DeleteWorkspaceMarketProductDataAsync(workspaceId, cancellationToken);
            await DeleteWorkspaceProductDataAsync(workspaceId, cancellationToken);
            await DeleteIfMappedAsync(
                () => _dbContext.Expenses.Where(x => x.IdWorkspace == workspaceId),
                cancellationToken);
            await DeleteIfMappedAsync(
                () => _dbContext.UserWorkspaces.Where(x => x.IdWorkspace == workspaceId),
                cancellationToken);

            _dbContext.Workspaces.Remove(workspace);
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }

        return ServiceResult.Success();
    }

    private Guid? GetCurrentUserId()
    {
        return _currentUser.IsAuthenticated ? _currentUser.UserId : null;
    }

    private async Task DeleteWorkspaceMarketProductDataAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (_dbContext.Model.FindEntityType(typeof(WorkspaceMarketProduct)) is null)
            return;

        var productIds = await _dbContext.WorkspaceMarketProducts
            .Where(x => x.IdWorkspace == workspaceId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var analysisRunIds = _dbContext.WorkspaceMarketProductAnalysisRuns
            .Where(x => x.IdWorkspace == workspaceId)
            .Select(x => x.Id);

        if (productIds.Count > 0)
        {
            await DeleteIfMappedAsync(
                () => _dbContext.WorkspaceMarketProductUserReadStates
                    .Where(x => productIds.Contains(x.IdWorkspaceMarketProduct)),
                cancellationToken);
            await DeleteIfMappedAsync(
                () => _dbContext.WorkspaceMarketProductMedia
                    .Where(x => productIds.Contains(x.IdWorkspaceMarketProduct)),
                cancellationToken);
            await DeleteIfMappedAsync(
                () => _dbContext.WorkspaceMarketProductAnalyses
                    .Where(x => productIds.Contains(x.IdWorkspaceMarketProduct) || analysisRunIds.Contains(x.IdAnalysisRun)),
                cancellationToken);
        }
        else
        {
            await DeleteIfMappedAsync(
                () => _dbContext.WorkspaceMarketProductAnalyses
                    .Where(x => analysisRunIds.Contains(x.IdAnalysisRun)),
                cancellationToken);
        }

        await DeleteIfMappedAsync(
            () => _dbContext.WorkspaceMarketProductAnalysisRuns
                .Where(x => x.IdWorkspace == workspaceId),
            cancellationToken);
        await DeleteIfMappedAsync(
            () => _dbContext.WorkspaceMarketProducts
                .Where(x => x.IdWorkspace == workspaceId),
            cancellationToken);
    }

    private async Task DeleteWorkspaceProductDataAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (_dbContext.Model.FindEntityType(typeof(Product)) is null)
            return;

        var productIds = await _dbContext.Products
            .Where(x => x.IdWorkspace == workspaceId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (productIds.Count == 0)
            return;

        await DeleteIfMappedAsync(
            () => _dbContext.ProductHistories
                .Where(x => productIds.Contains(x.IdProduct)),
            cancellationToken);
        await DeleteIfMappedAsync(
            () => _dbContext.ProductImages
                .Where(x => productIds.Contains(x.IdProduct)),
            cancellationToken);
        await DeleteIfMappedAsync(
            () => _dbContext.ProductVideos
                .Where(x => productIds.Contains(x.IdProduct)),
            cancellationToken);
        await DeleteIfMappedAsync(
            () => _dbContext.Products
                .Where(x => x.IdWorkspace == workspaceId),
            cancellationToken);
    }

    private async Task DeleteIfMappedAsync<TEntity>(
        Func<IQueryable<TEntity>> queryFactory,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (_dbContext.Model.FindEntityType(typeof(TEntity)) is null)
            return;

        var query = queryFactory();

        if (_dbContext.Database.IsRelational())
        {
            await query.ExecuteDeleteAsync(cancellationToken);
            return;
        }

        var entities = await query.ToListAsync(cancellationToken);
        if (entities.Count == 0)
            return;

        _dbContext.Set<TEntity>().RemoveRange(entities);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, IReadOnlyCollection<ManagementWorkspaceMemberResponse>>> LoadMembersAsync(
        IReadOnlyCollection<Guid> workspaceIds,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var memberRows = await _dbContext.UserWorkspaces
            .Where(membership => workspaceIds.Contains(membership.IdWorkspace))
            .Join(
                _dbContext.Users,
                membership => membership.IdUser,
                user => user.Id,
                (membership, user) => new { membership, user })
            .Join(
                _dbContext.Roles,
                row => row.membership.IdRole,
                role => role.Id,
                (row, role) => new
                {
                    row.membership.IdWorkspace,
                    row.user.Id,
                    row.user.Login,
                    row.user.Email,
                    row.membership.IdRole,
                    RoleName = role.Name
                })
            .OrderBy(x => x.Login)
            .ThenBy(x => x.Email)
            .ToListAsync(cancellationToken);

        return memberRows
            .GroupBy(x => x.IdWorkspace)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<ManagementWorkspaceMemberResponse>)group
                    .Select(x => new ManagementWorkspaceMemberResponse(
                        x.Id,
                        x.Login,
                        x.Email,
                        x.IdRole,
                        x.RoleName,
                        x.Id == currentUserId))
                    .ToList());
    }

    private static ManagementWorkspaceResponse ToResponse(
        Workspace workspace,
        IReadOnlyCollection<ManagementWorkspaceMemberResponse> members)
    {
        return new ManagementWorkspaceResponse(
            workspace.Id,
            workspace.IdBrand,
            workspace.Name,
            workspace.Description,
            workspace.Status,
            workspace.DateCreate,
            workspace.DateUpdate,
            members);
    }
}
