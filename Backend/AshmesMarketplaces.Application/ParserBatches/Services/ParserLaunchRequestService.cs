using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserLaunchRequestService : IParserLaunchRequestService
{
    private const string AdminRoleName = "Admin";

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ParserProductPresenceReconciliationService? _presenceReconciliationService;

    public ParserLaunchRequestService(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        ParserProductPresenceReconciliationService? presenceReconciliationService = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _presenceReconciliationService = presenceReconciliationService;
    }

    public async Task<ServiceResult<ParserLaunchRequestDto>> RequestLaunchAsync(
        Guid parserInstanceConfigurationId,
        CreateParserLaunchRequest request,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<ParserLaunchRequestDto>(access.Error!);

        if (_currentUser.UserId is null)
            return ServiceResult<ParserLaunchRequestDto>.Unauthorized("Authentication is required.");

        var mode = NormalizeMode(request.Mode);
        var validationError = ValidateRequest(mode, request.BatchLimit, request.ProxyKey);
        if (validationError is not null)
            return ServiceResult<ParserLaunchRequestDto>.BadRequest(validationError);

        var config = await _dbContext.ParserInstanceConfigurations
            .Include(x => x.ProxyAssignments.Where(a => a.Enabled))
            .ThenInclude(x => x.Proxy)
            .ThenInclude(x => x!.Assignment)
            .SingleOrDefaultAsync(
                x => x.Id == parserInstanceConfigurationId &&
                     x.Status == ParserInstanceConfigurationStatuses.Active,
                cancellationToken);
        if (config is null)
            return ServiceResult<ParserLaunchRequestDto>.NotFound("Parser instance configuration was not found.");

        var launchableProxyAssignments = config.ProxyAssignments
            .Where(x => x.Enabled &&
                        x.Proxy is { Status: ParserProxyStatuses.Active, Assignment.Enabled: true })
            .ToList();
        if (launchableProxyAssignments.Count == 0)
            return ServiceResult<ParserLaunchRequestDto>.BadRequest(
                "Инстанс не содержит proxy с назначенной активной нишей.");

        var proxyKey = string.IsNullOrWhiteSpace(request.ProxyKey) ? null : request.ProxyKey.Trim();
        if (mode == ParserLaunchModes.CheckProxy &&
            !launchableProxyAssignments.Any(x => string.Equals(x.Proxy!.Key, proxyKey, StringComparison.OrdinalIgnoreCase)))
        {
            return ServiceResult<ParserLaunchRequestDto>.BadRequest("Выбранный proxy не назначен этому инстансу.");
        }

        var hasActiveLaunch = await _dbContext.ParserLaunchRequests
            .AsNoTracking()
            .AnyAsync(
                x => x.ParserInstanceConfigurationId == config.Id &&
                     ParserLaunchRequestStatuses.Active.Contains(x.Status),
                cancellationToken);
        if (hasActiveLaunch)
            return ServiceResult<ParserLaunchRequestDto>.Conflict(
                "Для этого инстанса уже есть ожидающий или выполняющийся запуск.");

        var hasRunningProxyRun = await _dbContext.ParserProxyRuns
            .AsNoTracking()
            .AnyAsync(
                x => x.ParserInstanceId == config.ParserInstanceId &&
                     x.Status == ParserProxyRunStatuses.Running,
                cancellationToken);
        if (hasRunningProxyRun)
            return ServiceResult<ParserLaunchRequestDto>.Conflict(
                "У этого инстанса уже есть выполняющийся proxy-процесс.");

        var now = DateTime.UtcNow;
        var launch = new ParserLaunchRequest(
            config.Id,
            config.ParserInstanceId,
            mode,
            mode == ParserLaunchModes.CheckProxy ? proxyKey : null,
            mode == ParserLaunchModes.FullAll ? null : request.BatchLimit,
            _currentUser.UserId.Value,
            now);
        _dbContext.ParserLaunchRequests.Add(launch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ParserLaunchRequestDto>.Success(Map(launch));
    }

    public async Task<ParserLaunchRequest?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var launch = await _dbContext.ParserLaunchRequests
            .Where(x => x.Status == ParserLaunchRequestStatuses.Queued)
            .OrderBy(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (launch is null)
            return null;

        launch.MarkRunning(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return launch;
    }

    public async Task MarkCompletedAsync(Guid id, CancellationToken cancellationToken)
    {
        var launch = await _dbContext.ParserLaunchRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (launch is null)
            return;

        launch.MarkCompleted(DateTime.UtcNow);
        var parserCycleId = launch.ParserCycleId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (_presenceReconciliationService is not null)
            await _presenceReconciliationService.TryReconcileCycleAsync(parserCycleId, cancellationToken);
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken)
    {
        var launch = await _dbContext.ParserLaunchRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (launch is null)
            return;

        launch.MarkFailed(error, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? ValidateRequest(string mode, int? batchLimit, string? proxyKey)
    {
        return mode switch
        {
            ParserLaunchModes.LimitedAll when batchLimit is null or <= 0 =>
                "Для запуска с лимитом укажите положительное количество batch-ей.",
            ParserLaunchModes.LimitedAll when !string.IsNullOrWhiteSpace(proxyKey) =>
                "Для запуска всех proxy не нужно указывать proxy.",
            ParserLaunchModes.FullAll when batchLimit.HasValue =>
                "Для полной выгрузки лимит batch-ей не используется.",
            ParserLaunchModes.FullAll when !string.IsNullOrWhiteSpace(proxyKey) =>
                "Для полной выгрузки не нужно указывать proxy.",
            ParserLaunchModes.CheckProxy when string.IsNullOrWhiteSpace(proxyKey) =>
                "Для проверки proxy выберите proxy.",
            ParserLaunchModes.CheckProxy when batchLimit is null or <= 0 =>
                "Для проверки proxy укажите положительное количество batch-ей.",
            ParserLaunchModes.LimitedAll or ParserLaunchModes.FullAll or ParserLaunchModes.CheckProxy => null,
            _ => "Неизвестный режим запуска parser-а."
        };
    }

    private static string NormalizeMode(string mode)
    {
        return string.IsNullOrWhiteSpace(mode)
            ? string.Empty
            : mode.Trim().ToLowerInvariant();
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

    private static ParserLaunchRequestDto Map(ParserLaunchRequest launch) =>
        new(
            launch.Id,
            launch.ParserInstanceId,
            launch.LaunchMode,
            launch.ProxyKey,
            launch.BatchLimit,
            launch.Status,
            launch.RequestedAtUtc);
}
