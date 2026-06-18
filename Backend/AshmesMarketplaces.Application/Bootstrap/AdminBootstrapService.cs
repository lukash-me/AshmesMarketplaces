using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Bootstrap;

public sealed class AdminBootstrapService
{
    private const string AdminRoleName = "Admin";
    private const string DefaultPhone = "+70000000000";

    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHashService _passwordHashService;

    public AdminBootstrapService(ApplicationDbContext dbContext, IPasswordHashService passwordHashService)
    {
        _dbContext = dbContext;
        _passwordHashService = passwordHashService;
    }

    public async Task<AdminBootstrapResult> EnsureAdminAsync(
        string login,
        string password,
        string workspaceName,
        bool resetPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Admin login is required.", nameof(login));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Admin password is required.", nameof(password));

        if (string.IsNullOrWhiteSpace(workspaceName))
            throw new ArgumentException("Workspace name is required.", nameof(workspaceName));

        var normalizedLogin = login.Trim();
        var normalizedWorkspaceName = workspaceName.Trim();
        var nowUtc = DateTime.UtcNow;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var roleCreated = false;
        var role = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Name == AdminRoleName, cancellationToken);
        if (role is null)
        {
            role = new Role(AdminRoleName, "Production administrator", nowUtc, nowUtc);
            _dbContext.Roles.Add(role);
            roleCreated = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var userCreated = false;
        var passwordUpdated = false;
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Login == normalizedLogin, cancellationToken);
        if (user is null)
        {
            user = new User(
                role.Id,
                normalizedLogin,
                _passwordHashService.HashPassword(password),
                normalizedLogin,
                DefaultPhone,
                status: 1,
                nowUtc,
                nowUtc);
            _dbContext.Users.Add(user);
            userCreated = true;
            passwordUpdated = true;
        }
        else
        {
            var userEntry = _dbContext.Entry(user);
            userEntry.Property(x => x.IdRole).CurrentValue = role.Id;
            userEntry.Property(x => x.Status).CurrentValue = 1;
            userEntry.Property(x => x.Email).CurrentValue = normalizedLogin;

            if (resetPassword || !_passwordHashService.VerifyPassword(user.Password, password))
            {
                userEntry.Property(x => x.Password).CurrentValue = _passwordHashService.HashPassword(password);
                passwordUpdated = true;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var workspaceCreated = false;
        var workspace = await _dbContext.Workspaces.FirstOrDefaultAsync(
            x => x.Name == normalizedWorkspaceName,
            cancellationToken);
        if (workspace is null)
        {
            workspace = new Workspace(
                idBrand: null,
                normalizedWorkspaceName,
                "Production workspace",
                urlInvite: null,
                status: 1,
                nowUtc,
                nowUtc);
            _dbContext.Workspaces.Add(workspace);
            workspaceCreated = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var membershipCreated = false;
        var membership = await _dbContext.UserWorkspaces.FirstOrDefaultAsync(
            x => x.IdUser == user.Id && x.IdWorkspace == workspace.Id,
            cancellationToken);
        if (membership is null)
        {
            _dbContext.UserWorkspaces.Add(new UserWorkspace(user.Id, workspace.Id, role.Id));
            membershipCreated = true;
        }
        else if (membership.IdRole != role.Id)
        {
            _dbContext.Entry(membership).Property(x => x.IdRole).CurrentValue = role.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminBootstrapResult(
            normalizedLogin,
            user.Id,
            role.Id,
            workspace.Id,
            roleCreated,
            userCreated,
            passwordUpdated,
            workspaceCreated,
            membershipCreated);
    }
}
