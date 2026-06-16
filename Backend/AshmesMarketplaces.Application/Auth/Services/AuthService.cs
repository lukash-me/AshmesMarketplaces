using AshmesMarketplaces.Application.Auth.Dtos;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AshmesMarketplaces.Application.Auth.Services;

public sealed class AuthService : IAuthService
{
    private const string InvalidLoginMessage = "Invalid login or password.";
    private const string InvalidRefreshTokenMessage = "Invalid refresh token.";

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

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var login = request.Login.Trim();
        var users = await _dbContext.Users
            .Where(x => x.Login == login)
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
        var workspaces = await _dbContext.UserWorkspaces
            .AsNoTracking()
            .Where(x => x.IdUser == user.Id)
            .OrderBy(x => x.IdWorkspace)
            .Select(x => new AuthUserWorkspaceResponse(x.IdWorkspace, x.IdRole))
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
            user.Login,
            user.Email,
            user.Phone,
            user.Status,
            analysisSchedule,
            workspaces,
            permissions);
    }
}
