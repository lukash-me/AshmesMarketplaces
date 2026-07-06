using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketplaceCategories.Dtos;
using AshmesMarketplaces.Application.MarketplaceCategories.Services;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserProxyManagementService : IParserProxyManagementService
{
    private const string AdminRoleName = "Admin";
    private const string DefaultProxyKey = "local-proxy";

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IWildberriesCategoryCatalogService _categoryCatalogService;
    private readonly IParserProxySecretProtector _secretProtector;

    public ParserProxyManagementService(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IWildberriesCategoryCatalogService categoryCatalogService,
        IParserProxySecretProtector secretProtector)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _categoryCatalogService = categoryCatalogService;
        _secretProtector = secretProtector;
    }

    public async Task<ServiceResult<IReadOnlyList<ParserProxyDto>>> GetAdminListAsync(
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<IReadOnlyList<ParserProxyDto>>(access.Error!);

        var proxies = await _dbContext.ParserProxies
            .AsNoTracking()
            .Include(x => x.Assignment)
            .OrderBy(x => x.Key)
            .ToListAsync(cancellationToken);
        var assignedInstances = await ActiveAssignedInstancesAsync(
            proxies.Select(x => x.Id).ToList(),
            cancellationToken);

        return ServiceResult<IReadOnlyList<ParserProxyDto>>.Success(
            proxies.Select(x => MapAdmin(x, assignedInstances.GetValueOrDefault(x.Id))).ToList());
    }

    public async Task<ServiceResult<ParserProxyDto>> CreateAsync(
        CreateParserProxyRequest request,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<ParserProxyDto>(access.Error!);

        var validation = ValidateProxyFields(request.Ip, request.HttpPort, request.SocksPort, request.Login, request.Password, passwordRequired: true);
        if (validation is not null)
            return ServiceResult<ParserProxyDto>.BadRequest(validation);

        var now = DateTime.UtcNow;
        var key = await NextProxyKeyAsync(cancellationToken);
        var proxy = new ParserProxy(
            key,
            request.Ip!,
            request.HttpPort,
            request.SocksPort,
            request.Login!,
            _secretProtector.Protect(request.Password!),
            now);
        _dbContext.ParserProxies.Add(proxy);

        if (request.WbCategoryId.HasValue)
        {
            var categoryResult = await ResolveLeafCategoryAsync(request.WbCategoryId.Value, cancellationToken);
            if (!categoryResult.IsSuccess)
                return ServiceResult<ParserProxyDto>.BadRequest(categoryResult.Error!.Message);

            _dbContext.ParserProxyNicheAssignments.Add(CreateAssignment(proxy.Id, categoryResult.Value!, enabled: true, now));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await LoadProxyAsync(proxy.Id, cancellationToken);
        return ServiceResult<ParserProxyDto>.Success(MapAdmin(created!, null));
    }

    public async Task<ServiceResult<ParserProxyDto>> UpdateAsync(
        Guid id,
        UpdateParserProxyRequest request,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<ParserProxyDto>(access.Error!);

        var proxy = await _dbContext.ParserProxies
            .Include(x => x.Assignment)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (proxy is null)
            return ServiceResult<ParserProxyDto>.NotFound("Parser proxy was not found.");

        var validation = ValidateProxyFields(request.Ip, request.HttpPort, request.SocksPort, request.Login, request.Password, passwordRequired: false);
        if (validation is not null)
            return ServiceResult<ParserProxyDto>.BadRequest(validation);

        var now = DateTime.UtcNow;
        proxy.Update(
            request.Ip!,
            request.HttpPort,
            request.SocksPort,
            request.Login!,
            string.IsNullOrWhiteSpace(request.Password) ? null : _secretProtector.Protect(request.Password!),
            now);

        if (string.Equals(request.Status, ParserProxyStatuses.Inactive, StringComparison.OrdinalIgnoreCase))
        {
            proxy.Disable(now);
        }

        if (request.WbCategoryId.HasValue)
        {
            var categoryResult = await ResolveLeafCategoryAsync(request.WbCategoryId.Value, cancellationToken);
            if (!categoryResult.IsSuccess)
                return ServiceResult<ParserProxyDto>.BadRequest(categoryResult.Error!.Message);

            var enabled = request.AssignmentEnabled ?? true;
            if (proxy.Assignment is null)
            {
                _dbContext.ParserProxyNicheAssignments.Add(CreateAssignment(proxy.Id, categoryResult.Value!, enabled, now));
            }
            else
            {
                proxy.Assignment.Update(
                    categoryResult.Value!.Id,
                    categoryResult.Value.SourceCategory,
                    categoryResult.Value.SourceSubcategory,
                    categoryResult.Value.Path,
                    categoryResult.Value.SearchQuery ?? categoryResult.Value.Name,
                    categoryResult.Value.SourceSubcategory,
                    enabled,
                    now);
            }
        }
        else if (proxy.Assignment is not null)
        {
            _dbContext.ParserProxyNicheAssignments.Remove(proxy.Assignment);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await LoadProxyAsync(proxy.Id, cancellationToken);
        var assigned = await ActiveAssignedInstancesAsync([updated!.Id], cancellationToken);
        return ServiceResult<ParserProxyDto>.Success(MapAdmin(updated, assigned.GetValueOrDefault(updated.Id)));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return access;

        var proxy = await _dbContext.ParserProxies
            .Include(x => x.Assignment)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (proxy is null)
            return ServiceResult.NotFound("Parser proxy was not found.");

        proxy.Disable(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<ParserRuntimeProxyAssignmentsDto>> GetRuntimeAssignmentsAsync(
        string? parserInstanceId,
        CancellationToken cancellationToken)
    {
        var normalizedParserInstanceId = parserInstanceId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedParserInstanceId))
            return ServiceResult<ParserRuntimeProxyAssignmentsDto>.BadRequest("Parser instance id is required.");

        var instance = await _dbContext.ParserInstanceConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.ParserInstanceId == normalizedParserInstanceId &&
                     x.Status == ParserInstanceConfigurationStatuses.Active,
                cancellationToken);
        if (instance is null)
            return ServiceResult<ParserRuntimeProxyAssignmentsDto>.NotFound(
                $"Parser instance '{normalizedParserInstanceId}' is not configured.");

        var assignedProxyIds = await _dbContext.ParserInstanceProxyAssignments
            .AsNoTracking()
            .Where(x => x.ParserInstanceConfigurationId == instance.Id && x.Enabled)
            .Select(x => x.ProxyId)
            .ToListAsync(cancellationToken);
        if (assignedProxyIds.Count == 0)
            return ServiceResult<ParserRuntimeProxyAssignmentsDto>.NotFound(
                $"Parser instance '{normalizedParserInstanceId}' has no assigned proxy.");

        var proxies = await _dbContext.ParserProxies
            .AsNoTracking()
            .Include(x => x.Assignment)
            .Where(x => assignedProxyIds.Contains(x.Id) && x.Status == ParserProxyStatuses.Active)
            .OrderBy(x => x.Key)
            .ToListAsync(cancellationToken);

        var launchable = proxies
            .Where(x => x.Assignment is not null && x.Assignment.Enabled)
            .ToList();

        if (launchable.Count == 0)
            return ServiceResult<ParserRuntimeProxyAssignmentsDto>.NotFound(
                "Нет настроенных proxy с назначенными нишами. Добавьте proxy через интерфейс сервиса.");

        var runtimeProxies = new List<ParserRuntimeProxyDefinitionDto>();
        var runtimeNiches = new List<ParserRuntimeNicheAssignmentDto>();
        foreach (var proxy in launchable)
        {
            var password = _secretProtector.Unprotect(proxy.EncryptedPassword);
            runtimeProxies.Add(new ParserRuntimeProxyDefinitionDto(
                proxy.Key,
                "http-proxy",
                $"http://{proxy.Ip}:{proxy.HttpPort}",
                $"socks5://{proxy.Ip}:{proxy.SocksPort}",
                new ParserRuntimeProxyCredentialsDto(proxy.Login, password)));

            var assignment = proxy.Assignment!;
            runtimeNiches.Add(new ParserRuntimeNicheAssignmentDto(
                assignment.WbCategoryId,
                assignment.SourceCategory,
                assignment.SourceSubcategory,
                assignment.SourcePath,
                assignment.SearchQuery,
                assignment.ParserSearchText,
                proxy.Key,
                assignment.Enabled));
        }

        var defaultProxy = new ParserRuntimeProxyDefinitionDto(DefaultProxyKey, "direct", null, null, null);
        return ServiceResult<ParserRuntimeProxyAssignmentsDto>.Success(
            new ParserRuntimeProxyAssignmentsDto(defaultProxy, runtimeProxies, runtimeNiches));
    }

    public async Task<ServiceResult<ParserRuntimeReviewSyncStateDto>> GetRuntimeReviewSyncStateAsync(
        string? wbProductId,
        CancellationToken cancellationToken)
    {
        var normalizedProductId = wbProductId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProductId))
            return ServiceResult<ParserRuntimeReviewSyncStateDto>.BadRequest("WB product id is required.");

        var summary = await _dbContext.ParserCurrentProductReviewsSummaries
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.WbProductId == normalizedProductId, cancellationToken);

        var reviewRows = await _dbContext.ParserReviewRows
            .AsNoTracking()
            .Where(x => x.Marketplace == "wildberries" && x.WbProductId == normalizedProductId)
            .Select(x => new { x.ReviewIdOnMp, x.CreatedAtOnMp })
            .ToListAsync(cancellationToken);

        var knownReviewIds = reviewRows
            .Select(x => x.ReviewIdOnMp)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var repliedReviewIds = await _dbContext.ParserReviewReplyRows
            .AsNoTracking()
            .Where(x => x.Marketplace == "wildberries" && x.WbProductId == normalizedProductId)
            .Select(x => x.ReviewIdOnMp)
            .Distinct()
            .ToListAsync(cancellationToken);
        var replied = repliedReviewIds.ToHashSet(StringComparer.Ordinal);
        var unansweredReviewIds = knownReviewIds
            .Where(x => !replied.Contains(x))
            .ToList();

        var latestReviewDateUtc = reviewRows
            .Select(x => x.CreatedAtOnMp)
            .Where(x => x.HasValue)
            .DefaultIfEmpty()
            .Max();

        return ServiceResult<ParserRuntimeReviewSyncStateDto>.Success(
            new ParserRuntimeReviewSyncStateDto(
                normalizedProductId,
                summary?.MarketplaceFeedbackCount,
                summary?.FetchedReviewsCount ?? knownReviewIds.Count,
                summary?.CoverageStatus ?? "unknown",
                latestReviewDateUtc,
                knownReviewIds,
                unansweredReviewIds));
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
            : ServiceResult.Forbidden("Administrator role is required.");
    }

    private static ServiceResult<T> AccessError<T>(ServiceError error)
    {
        return error.Type switch
        {
            ServiceErrorType.Unauthorized => ServiceResult<T>.Unauthorized(error.Message),
            ServiceErrorType.Forbidden => ServiceResult<T>.Forbidden(error.Message),
            _ => ServiceResult<T>.BadRequest(error.Message)
        };
    }

    private static string? ValidateProxyFields(
        string? ip,
        int httpPort,
        int socksPort,
        string? login,
        string? password,
        bool passwordRequired)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return "Proxy IP is required.";
        if (httpPort is < 1 or > 65535)
            return "HTTP port must be between 1 and 65535.";
        if (socksPort is < 1 or > 65535)
            return "SOCKS5 port must be between 1 and 65535.";
        if (string.IsNullOrWhiteSpace(login))
            return "Proxy login is required.";
        if (passwordRequired && string.IsNullOrWhiteSpace(password))
            return "Proxy password is required.";

        return null;
    }

    private async Task<string> NextProxyKeyAsync(CancellationToken cancellationToken)
    {
        var keys = await _dbContext.ParserProxies
            .AsNoTracking()
            .Select(x => x.Key)
            .ToListAsync(cancellationToken);

        var max = keys
            .Select(x => x.StartsWith("proxy-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(x["proxy-".Length..], out var number)
                    ? number
                    : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"proxy-{max + 1}";
    }

    private async Task<ServiceResult<WildberriesCategoryNodeDto>> ResolveLeafCategoryAsync(
        long wbCategoryId,
        CancellationToken cancellationToken)
    {
        var leavesResult = await _categoryCatalogService.GetLeavesAsync(cancellationToken);
        if (!leavesResult.IsSuccess)
            return ServiceResult<WildberriesCategoryNodeDto>.Unavailable(leavesResult.Error!.Message);

        var leaf = leavesResult.Value!.SingleOrDefault(x => x.Id == wbCategoryId);
        if (leaf is null)
            return ServiceResult<WildberriesCategoryNodeDto>.NotFound("WB leaf category was not found.");
        if (!leaf.IsLeaf)
            return ServiceResult<WildberriesCategoryNodeDto>.BadRequest("WB category must be a leaf category.");
        if (string.IsNullOrWhiteSpace(leaf.SearchQuery))
            return ServiceResult<WildberriesCategoryNodeDto>.BadRequest("WB leaf category does not have a search query.");

        return ServiceResult<WildberriesCategoryNodeDto>.Success(leaf);
    }

    private static ParserProxyNicheAssignment CreateAssignment(
        Guid proxyId,
        WildberriesCategoryNodeDto category,
        bool enabled,
        DateTime nowUtc)
    {
        return new ParserProxyNicheAssignment(
            proxyId,
            category.Id,
            category.SourceCategory,
            category.SourceSubcategory,
            category.Path,
            category.SearchQuery ?? category.Name,
            category.SourceSubcategory,
            enabled,
            nowUtc);
    }

    private async Task<ParserProxy?> LoadProxyAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.ParserProxies
            .AsNoTracking()
            .Include(x => x.Assignment)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, ParserProxyAssignedInstanceDto>> ActiveAssignedInstancesAsync(
        IReadOnlyCollection<Guid> proxyIds,
        CancellationToken cancellationToken)
    {
        if (proxyIds.Count == 0)
            return new Dictionary<Guid, ParserProxyAssignedInstanceDto>();

        return await _dbContext.ParserInstanceProxyAssignments
            .AsNoTracking()
            .Include(x => x.ParserInstanceConfiguration)
            .Where(x => x.Enabled &&
                        proxyIds.Contains(x.ProxyId) &&
                        x.ParserInstanceConfiguration!.Status == ParserInstanceConfigurationStatuses.Active)
            .ToDictionaryAsync(
                x => x.ProxyId,
                x => new ParserProxyAssignedInstanceDto(
                    x.ParserInstanceConfigurationId,
                    x.ParserInstanceConfiguration!.ParserInstanceId,
                    x.ParserInstanceConfiguration.DisplayName),
                cancellationToken);
    }

    private static ParserProxyDto MapAdmin(ParserProxy proxy, ParserProxyAssignedInstanceDto? assignedInstance)
    {
        return new ParserProxyDto(
            proxy.Id,
            proxy.Key,
            proxy.Ip,
            proxy.HttpPort,
            proxy.SocksPort,
            proxy.Login,
            !string.IsNullOrWhiteSpace(proxy.EncryptedPassword),
            proxy.Status,
            proxy.CreatedAtUtc,
            proxy.UpdatedAtUtc,
            proxy.Assignment is null
                ? null
                : new ParserProxyAssignmentDto(
                    proxy.Assignment.Id,
                    proxy.Assignment.WbCategoryId,
                    proxy.Assignment.SourceCategory,
                    proxy.Assignment.SourceSubcategory,
                    proxy.Assignment.SourcePath,
                    proxy.Assignment.SearchQuery,
                    proxy.Assignment.ParserSearchText,
                    proxy.Assignment.Enabled),
            assignedInstance);
    }
}
