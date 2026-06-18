using AshmesMarketplaces.Application.Auth.Dtos;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Users;

namespace AshmesMarketplaces.Application.Auth.Services;

public sealed class AccessRequestService : IAccessRequestService
{
    private readonly ApplicationDbContext _dbContext;

    public AccessRequestService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<AccessRequestResponse>> CreateAsync(
        CreateAccessRequestRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var contact = request.Contact.Trim();
        if (string.IsNullOrWhiteSpace(contact))
            return ServiceResult<AccessRequestResponse>.BadRequest("Contact is required.");

        var nowUtc = DateTime.UtcNow;
        var accessRequest = new AccessRequest(
            contact,
            request.Comment ?? string.Empty,
            ipAddress,
            userAgent,
            request.SourcePath,
            nowUtc);

        _dbContext.AccessRequests.Add(accessRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AccessRequestResponse>.Success(new AccessRequestResponse(
            accessRequest.Id,
            accessRequest.Contact,
            accessRequest.Status,
            accessRequest.CreatedAtUtc));
    }
}
