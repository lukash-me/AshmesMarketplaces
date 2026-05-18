using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Users.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Users.Services;

public sealed class UserService : IUserService
{
    private readonly ApplicationDbContext _dbContext;

    public UserService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<UserListItemResponse>>> GetListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var users = _dbContext.Users.AsNoTracking();

        if (query.IdRole.HasValue)
            users = users.Where(x => x.IdRole == query.IdRole.Value);

        if (query.Status.HasValue)
            users = users.Where(x => x.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            users = users.Where(x =>
                EF.Functions.ILike(x.Login, $"%{search}%")
                || (x.Email != null && EF.Functions.ILike(x.Email, $"%{search}%"))
                || EF.Functions.ILike(x.Phone, $"%{search}%"));
        }

        users = query.Sort?.Trim() switch
        {
            "login" => users.OrderBy(x => x.Login),
            "-login" => users.OrderByDescending(x => x.Login),
            "dateCreate" => users.OrderBy(x => x.DateCreate),
            "-dateCreate" => users.OrderByDescending(x => x.DateCreate),
            "dateLogin" => users.OrderBy(x => x.DateLogin),
            "-dateLogin" => users.OrderByDescending(x => x.DateLogin),
            _ => users.OrderBy(x => x.Login)
        };

        var totalCount = await users.CountAsync(cancellationToken);
        var items = await users
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UserListItemResponse(
                x.Id,
                x.IdRole,
                x.Login,
                x.Email,
                x.Phone,
                x.Status,
                x.DateCreate,
                x.DateLogin))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<UserListItemResponse>>.Success(new PagedResponse<UserListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<UserResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<UserResponse>.BadRequest("User id is required.");

        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return user is null
            ? ServiceResult<UserResponse>.NotFound("User was not found.")
            : ServiceResult<UserResponse>.Success(MapToResponse(user));
    }

    public async Task<ServiceResult<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!await RoleExistsAsync(request.IdRole, cancellationToken))
            return ServiceResult<UserResponse>.Conflict("Role was not found.");

        try
        {
            var user = new User(
                request.IdRole,
                request.Login,
                request.Password,
                request.Email,
                request.Phone,
                request.Status,
                request.DateCreate,
                request.DateLogin);

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<UserResponse>.Success(MapToResponse(user));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<UserResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<UserResponse>.Conflict("User cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<UserResponse>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<UserResponse>.BadRequest("User id is required.");

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
            return ServiceResult<UserResponse>.NotFound("User was not found.");

        if (!await RoleExistsAsync(request.IdRole, cancellationToken))
            return ServiceResult<UserResponse>.Conflict("Role was not found.");

        try
        {
            _ = new User(
                request.IdRole,
                request.Login,
                request.Password,
                request.Email,
                request.Phone,
                request.Status,
                request.DateCreate,
                request.DateLogin);

            var entry = _dbContext.Entry(user);
            entry.Property(x => x.IdRole).CurrentValue = request.IdRole;
            entry.Property(x => x.Login).CurrentValue = request.Login;
            entry.Property(x => x.Password).CurrentValue = request.Password;
            entry.Property(x => x.Email).CurrentValue = request.Email;
            entry.Property(x => x.Phone).CurrentValue = request.Phone;
            entry.Property(x => x.Status).CurrentValue = request.Status;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateLogin).CurrentValue = request.DateLogin;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<UserResponse>.Success(MapToResponse(user));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<UserResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<UserResponse>.Conflict("User cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("User id is required.");

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
            return ServiceResult.NotFound("User was not found.");

        _dbContext.Users.Remove(user);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("User cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<bool> RoleExistsAsync(Guid idRole, CancellationToken cancellationToken)
    {
        return await _dbContext.Roles.AsNoTracking().AnyAsync(x => x.Id == idRole, cancellationToken);
    }

    private static UserResponse MapToResponse(User user)
    {
        return new UserResponse(
            user.Id,
            user.IdRole,
            user.Login,
            user.Email,
            user.Phone,
            user.Status,
            user.DateCreate,
            user.DateLogin);
    }
}
