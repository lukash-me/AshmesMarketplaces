using AshmesMarketplaces.Application.Auth.Dtos;
using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.Auth.Services;

public interface IAuthService
{
    Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<ServiceResult<LoginResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> LogoutAsync(CancellationToken cancellationToken);
    Task<ServiceResult<AuthUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken);
}
