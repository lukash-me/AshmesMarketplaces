using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Users.Dtos;

namespace AshmesMarketplaces.Application.Users.Services;

public interface IUserService
{
    Task<ServiceResult<PagedResponse<UserListItemResponse>>> GetListAsync(UserListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<UserResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<UserResponse>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
