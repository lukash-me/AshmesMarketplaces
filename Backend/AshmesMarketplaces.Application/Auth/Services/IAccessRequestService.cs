using AshmesMarketplaces.Application.Auth.Dtos;
using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.Auth.Services;

public interface IAccessRequestService
{
    Task<ServiceResult<AccessRequestResponse>> CreateAsync(
        CreateAccessRequestRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);
}
