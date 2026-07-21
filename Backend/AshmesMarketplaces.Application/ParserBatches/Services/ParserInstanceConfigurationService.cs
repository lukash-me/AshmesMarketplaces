using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserInstanceConfigurationService : IParserInstanceConfigurationService
{
    private const string AdminRoleName = "Admin";
    private const string ParserInstancePrefix = "parser-local";

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ParserInstanceConfigurationService(ApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<ParserInstanceConfigurationDto>>> GetAdminListAsync(
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<IReadOnlyList<ParserInstanceConfigurationDto>>(access.Error!);

        var configs = await LoadConfigurations()
            .OrderBy(x => x.ParserInstanceId)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<ParserInstanceConfigurationDto>>.Success(configs.Select(Map).ToList());
    }

    public async Task<ServiceResult<ParserInstanceConfigurationDto>> CreateAsync(
        CreateParserInstanceConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<ParserInstanceConfigurationDto>(access.Error!);

        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return ServiceResult<ParserInstanceConfigurationDto>.BadRequest("Название инстанса обязательно.");

        var proxyIds = NormalizeProxyIds(request.ProxyIds);
        var validation = await ValidateProxyAssignmentsAsync(proxyIds, currentConfigurationId: null, cancellationToken);
        if (!validation.IsSuccess)
            return ToTypedError<ParserInstanceConfigurationDto>(validation.Error!);

        var now = DateTime.UtcNow;
        var parserInstanceId = await NextParserInstanceIdAsync(cancellationToken);
        var config = new ParserInstanceConfiguration(
            parserInstanceId,
            displayName,
            ParserInstanceHostKinds.Local,
            now);
        _dbContext.ParserInstanceConfigurations.Add(config);
        foreach (var proxyId in proxyIds)
        {
            _dbContext.ParserInstanceProxyAssignments.Add(new ParserInstanceProxyAssignment(config.Id, proxyId, now));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await LoadConfigurations().SingleAsync(x => x.Id == config.Id, cancellationToken);
        return ServiceResult<ParserInstanceConfigurationDto>.Success(Map(created));
    }

    public async Task<ServiceResult<ParserInstanceConfigurationDto>> UpdateAsync(
        Guid id,
        UpdateParserInstanceConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<ParserInstanceConfigurationDto>(access.Error!);

        var config = await _dbContext.ParserInstanceConfigurations
            .Include(x => x.ProxyAssignments)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (config is null)
            return ServiceResult<ParserInstanceConfigurationDto>.NotFound("Parser instance configuration was not found.");

        var now = DateTime.UtcNow;
        if (string.Equals(request.Status, ParserInstanceConfigurationStatuses.Inactive, StringComparison.OrdinalIgnoreCase))
        {
            config.Disable(now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            var disabled = await LoadConfigurations().SingleAsync(x => x.Id == id, cancellationToken);
            return ServiceResult<ParserInstanceConfigurationDto>.Success(Map(disabled));
        }

        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return ServiceResult<ParserInstanceConfigurationDto>.BadRequest("Название инстанса обязательно.");

        var proxyIds = NormalizeProxyIds(request.ProxyIds);
        var validation = await ValidateProxyAssignmentsAsync(proxyIds, id, cancellationToken);
        if (!validation.IsSuccess)
            return ToTypedError<ParserInstanceConfigurationDto>(validation.Error!);

        config.Update(displayName, now);
        _dbContext.ParserInstanceProxyAssignments.RemoveRange(config.ProxyAssignments);
        foreach (var proxyId in proxyIds)
        {
            _dbContext.ParserInstanceProxyAssignments.Add(new ParserInstanceProxyAssignment(config.Id, proxyId, now));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await LoadConfigurations().SingleAsync(x => x.Id == id, cancellationToken);
        return ServiceResult<ParserInstanceConfigurationDto>.Success(Map(updated));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return access;

        var config = await _dbContext.ParserInstanceConfigurations
            .Include(x => x.ProxyAssignments)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (config is null)
            return ServiceResult.NotFound("Parser instance configuration was not found.");

        config.Disable(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    private IQueryable<ParserInstanceConfiguration> LoadConfigurations()
    {
        return _dbContext.ParserInstanceConfigurations
            .AsNoTracking()
            .Include(x => x.ProxyAssignments.Where(a => a.Enabled))
            .ThenInclude(x => x.Proxy)
            .ThenInclude(x => x!.Assignment);
    }

    private async Task<ServiceResult> ValidateProxyAssignmentsAsync(
        IReadOnlyCollection<Guid> proxyIds,
        Guid? currentConfigurationId,
        CancellationToken cancellationToken)
    {
        if (proxyIds.Count == 0)
            return ServiceResult.Success();

        var proxies = await _dbContext.ParserProxies
            .AsNoTracking()
            .Where(x => proxyIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Key, x.Status })
            .ToListAsync(cancellationToken);
        if (proxies.Count != proxyIds.Count)
            return ServiceResult.BadRequest("Один или несколько proxy не найдены.");

        var inactive = proxies.FirstOrDefault(x => x.Status != ParserProxyStatuses.Active);
        if (inactive is not null)
            return ServiceResult.BadRequest($"Proxy {inactive.Key} не активен.");

        var alreadyAssigned = await _dbContext.ParserInstanceProxyAssignments
            .AsNoTracking()
            .Include(x => x.ParserInstanceConfiguration)
            .Include(x => x.Proxy)
            .Where(x => x.Enabled &&
                        proxyIds.Contains(x.ProxyId) &&
                        x.ParserInstanceConfiguration!.Status == ParserInstanceConfigurationStatuses.Active &&
                        (!currentConfigurationId.HasValue || x.ParserInstanceConfigurationId != currentConfigurationId.Value))
            .Select(x => new
            {
                ProxyKey = x.Proxy!.Key,
                InstanceName = x.ParserInstanceConfiguration!.DisplayName
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (alreadyAssigned is not null)
            return ServiceResult.Conflict($"Proxy {alreadyAssigned.ProxyKey} уже назначен инстансу {alreadyAssigned.InstanceName}.");

        return ServiceResult.Success();
    }

    private async Task<string> NextParserInstanceIdAsync(CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ParserInstanceConfigurations
            .AsNoTracking()
            .Select(x => x.ParserInstanceId)
            .ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < 10000; index++)
        {
            var candidate = $"{ParserInstancePrefix}-{index:00}";
            if (!existingSet.Contains(candidate))
                return candidate;
        }

        throw new InvalidOperationException("Unable to generate parser instance id.");
    }

    private static IReadOnlyList<Guid> NormalizeProxyIds(IReadOnlyList<Guid>? proxyIds)
    {
        return (proxyIds ?? [])
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private async Task<ServiceResult> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.RoleId is null)
            return ServiceResult.Unauthorized("Authentication is required.");

        var roleName = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.Id == _currentUser.RoleId)
            .Select(x => x.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return string.Equals(roleName, AdminRoleName, StringComparison.OrdinalIgnoreCase)
            ? ServiceResult.Success()
            : ServiceResult.Forbidden("Admin role is required.");
    }

    private static ServiceResult<T> AccessError<T>(ServiceError error)
    {
        return error.Type switch
        {
            ServiceErrorType.Unauthorized => ServiceResult<T>.Unauthorized(error.Message),
            ServiceErrorType.Forbidden => ServiceResult<T>.Forbidden(error.Message),
            _ => ServiceResult<T>.Forbidden(error.Message)
        };
    }

    private static ServiceResult<T> ToTypedError<T>(ServiceError error)
    {
        return error.Type switch
        {
            ServiceErrorType.BadRequest => ServiceResult<T>.BadRequest(error.Message),
            ServiceErrorType.Conflict => ServiceResult<T>.Conflict(error.Message),
            ServiceErrorType.NotFound => ServiceResult<T>.NotFound(error.Message),
            _ => ServiceResult<T>.BadRequest(error.Message)
        };
    }

    private static ParserInstanceConfigurationDto Map(ParserInstanceConfiguration config)
    {
        var proxies = config.ProxyAssignments
            .Where(x => x.Enabled && x.Proxy is not null)
            .OrderBy(x => x.Proxy!.Key)
            .Select(x => new ParserInstanceConfiguredProxyDto(
                x.ProxyId,
                x.Proxy!.Key,
                x.Proxy.Ip,
                x.Proxy.Assignment?.SourceSubcategory,
                x.Proxy.Assignment?.Enabled ?? false))
            .ToList();

        return new ParserInstanceConfigurationDto(
            config.Id,
            config.ParserInstanceId,
            config.DisplayName,
            config.HostKind,
            config.Status,
            config.CreatedAtUtc,
            config.UpdatedAtUtc,
            proxies);
    }
}
