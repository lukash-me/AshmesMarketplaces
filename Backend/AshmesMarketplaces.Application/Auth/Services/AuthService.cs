using AshmesMarketplaces.Application.Auth.Dtos;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AshmesMarketplaces.Application.Auth.Services;

public sealed class AuthService : IAuthService
{
    private const string InvalidLoginMessage = "Invalid login or password.";
    private const string InvalidRefreshTokenMessage = "Invalid refresh token.";
    private const string RegisteredUserRoleName = "Manager";
    private const string RegisteredUserRoleDescription = "Registered user";

    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IUserAnalysisScheduleService _analysisScheduleService;
    private readonly ICurrentUser _currentUser;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        ApplicationDbContext dbContext,
        IPasswordHashService passwordHashService,
        IRefreshTokenService refreshTokenService,
        IAccessTokenService accessTokenService,
        IUserAnalysisScheduleService analysisScheduleService,
        ICurrentUser currentUser,
        IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _passwordHashService = passwordHashService;
        _refreshTokenService = refreshTokenService;
        _accessTokenService = accessTokenService;
        _analysisScheduleService = analysisScheduleService;
        _currentUser = currentUser;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<ServiceResult<LoginResponse>> RegisterAsync(
        RegisterRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return ServiceResult<LoginResponse>.BadRequest("Email is required.");

        var existingUser = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Login == email || x.Email == email, cancellationToken);
        if (existingUser)
            return ServiceResult<LoginResponse>.Conflict("User with this email already exists.");

        var nowUtc = DateTime.UtcNow;
        var passwordHash = _passwordHashService.HashPassword(request.Password);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var role = await _dbContext.Roles
                .FirstOrDefaultAsync(x => x.Name == RegisteredUserRoleName, cancellationToken);
            if (role is null)
            {
                role = new Role(RegisteredUserRoleName, RegisteredUserRoleDescription, nowUtc, nowUtc);
                _dbContext.Roles.Add(role);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            var user = new User(
                role.Id,
                email,
                passwordHash,
                email,
                "not-provided",
                status: 1,
                nowUtc,
                nowUtc);

            var workspace = new Workspace(
                idBrand: null,
                name: "Первое пространство",
                description: null,
                urlInvite: null,
                status: 1,
                nowUtc,
                nowUtc);

            _dbContext.Users.Add(user);
            _dbContext.Workspaces.Add(workspace);
            _dbContext.UserWorkspaces.Add(new UserWorkspace(user.Id, workspace.Id, role.Id));

            var refreshToken = _refreshTokenService.CreateRefreshToken();
            var refreshTokenHash = _refreshTokenService.HashRefreshToken(refreshToken);
            var refreshTokenExpiresAtUtc = nowUtc.AddDays(_jwtOptions.RefreshTokenLifetimeDays);
            var session = new Session(
                user.Id,
                ipAddress,
                userAgent,
                refreshTokenHash,
                SessionStatuses.Active,
                nowUtc,
                null,
                refreshTokenExpiresAtUtc);

            _dbContext.Sessions.Add(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var accessToken = _accessTokenService.CreateAccessToken(user, session.Id, nowUtc);
            var response = await CreateLoginResponseAsync(user, session.Id, refreshToken, refreshTokenExpiresAtUtc, accessToken, cancellationToken);
            return ServiceResult<LoginResponse>.Success(response);
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<LoginResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<LoginResponse>.Conflict("User cannot be registered because it conflicts with existing data.");
        }
    }

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var login = request.Login.Trim().ToLowerInvariant();
        var users = await _dbContext.Users
            .Where(x => x.Login == login || x.Email == login)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (users.Count != 1)
            return ServiceResult<LoginResponse>.Unauthorized(InvalidLoginMessage);

        var user = users[0];
        if (!_passwordHashService.VerifyPassword(user.Password, request.Password))
            return ServiceResult<LoginResponse>.Unauthorized(InvalidLoginMessage);

        var nowUtc = DateTime.UtcNow;
        var refreshToken = _refreshTokenService.CreateRefreshToken();
        var refreshTokenHash = _refreshTokenService.HashRefreshToken(refreshToken);
        var refreshTokenExpiresAtUtc = nowUtc.AddDays(_jwtOptions.RefreshTokenLifetimeDays);
        var session = new Session(
            user.Id,
            ipAddress,
            userAgent,
            refreshTokenHash,
            SessionStatuses.Active,
            nowUtc,
            null,
            refreshTokenExpiresAtUtc);

        _dbContext.Sessions.Add(session);
        _dbContext.Entry(user).Property(x => x.DateLogin).CurrentValue = nowUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = _accessTokenService.CreateAccessToken(user, session.Id, nowUtc);
        var response = await CreateLoginResponseAsync(user, session.Id, refreshToken, refreshTokenExpiresAtUtc, accessToken, cancellationToken);
        return ServiceResult<LoginResponse>.Success(response);
    }

    public async Task<ServiceResult<LoginResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var session = await _dbContext.Sessions.FirstOrDefaultAsync(x => x.Id == request.SessionId, cancellationToken);
        if (session is null
            || session.Status != SessionStatuses.Active
            || session.DateExpires <= nowUtc
            || !_refreshTokenService.VerifyRefreshToken(session.Token, request.RefreshToken))
        {
            return ServiceResult<LoginResponse>.Unauthorized(InvalidRefreshTokenMessage);
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == session.IdUser, cancellationToken);
        if (user is null)
            return ServiceResult<LoginResponse>.Unauthorized(InvalidRefreshTokenMessage);

        var refreshToken = _refreshTokenService.CreateRefreshToken();
        var refreshTokenHash = _refreshTokenService.HashRefreshToken(refreshToken);
        var refreshTokenExpiresAtUtc = nowUtc.AddDays(_jwtOptions.RefreshTokenLifetimeDays);

        var sessionEntry = _dbContext.Entry(session);
        sessionEntry.Property(x => x.Token).CurrentValue = refreshTokenHash;
        sessionEntry.Property(x => x.DateRefreshed).CurrentValue = nowUtc;
        sessionEntry.Property(x => x.DateExpires).CurrentValue = refreshTokenExpiresAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = _accessTokenService.CreateAccessToken(user, session.Id, nowUtc);
        var response = await CreateLoginResponseAsync(user, session.Id, refreshToken, refreshTokenExpiresAtUtc, accessToken, cancellationToken);
        return ServiceResult<LoginResponse>.Success(response);
    }

    public async Task<ServiceResult> LogoutAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.SessionId.HasValue)
            return ServiceResult.Unauthorized("Authentication is required.");

        var session = await _dbContext.Sessions.FirstOrDefaultAsync(x => x.Id == _currentUser.SessionId.Value, cancellationToken);
        if (session is null)
            return ServiceResult.Unauthorized("Authentication is required.");

        var nowUtc = DateTime.UtcNow;
        var sessionEntry = _dbContext.Entry(session);
        sessionEntry.Property(x => x.Status).CurrentValue = SessionStatuses.Revoked;
        sessionEntry.Property(x => x.DateRefreshed).CurrentValue = nowUtc;
        sessionEntry.Property(x => x.DateExpires).CurrentValue = nowUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<AuthUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            return ServiceResult<AuthUserResponse>.Unauthorized("Authentication is required.");

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == _currentUser.UserId.Value, cancellationToken);

        if (user is null)
            return ServiceResult<AuthUserResponse>.Unauthorized("Authentication is required.");

        var response = await MapToAuthUserResponseAsync(user, cancellationToken);
        return ServiceResult<AuthUserResponse>.Success(response);
    }

    private async Task<LoginResponse> CreateLoginResponseAsync(
        User user,
        int sessionId,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc,
        AccessTokenResult accessToken,
        CancellationToken cancellationToken)
    {
        var authUser = await MapToAuthUserResponseAsync(user, cancellationToken);
        return new LoginResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshToken,
            refreshTokenExpiresAtUtc,
            sessionId,
            authUser);
    }

    private async Task<AuthUserResponse> MapToAuthUserResponseAsync(User user, CancellationToken cancellationToken)
    {
        var analysisSchedule = await _analysisScheduleService.EnsureScheduleAsync(user.Id, cancellationToken);
        var roleName = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.Id == user.IdRole)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var workspaces = await _dbContext.UserWorkspaces
            .AsNoTracking()
            .Where(x => x.IdUser == user.Id)
            .Join(
                _dbContext.Workspaces.AsNoTracking(),
                membership => membership.IdWorkspace,
                workspace => workspace.Id,
                (membership, workspace) => new { membership, workspace })
            .OrderBy(x => x.workspace.Name)
            .ThenBy(x => x.workspace.Id)
            .Select(x => new AuthUserWorkspaceResponse(
                x.membership.IdWorkspace,
                x.membership.IdRole,
                x.workspace.Name))
            .ToListAsync(cancellationToken);

        var permissions = await (
                from rolePermission in _dbContext.RolePermissions.AsNoTracking()
                join permission in _dbContext.Permissions.AsNoTracking()
                    on rolePermission.IdPermission equals permission.Id
                where rolePermission.IdRole == user.IdRole
                orderby permission.Name
                select new AuthPermissionResponse(
                    permission.Id,
                    permission.IdCategory,
                    permission.Name,
                    permission.Description,
                    permission.Domain))
            .ToListAsync(cancellationToken);

        return new AuthUserResponse(
            user.Id,
            user.IdRole,
            roleName,
            user.Login,
            user.Email,
            user.Phone,
            user.Status,
            analysisSchedule,
            workspaces,
            permissions);
    }
}
