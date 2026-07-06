using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserAdminMonitoringService : IParserAdminMonitoringService
{
    private const string AdminRoleName = "Admin";
    private const int MaxPageSize = 200;

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ParserAdminMonitoringService(ApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<ParserAdminInstanceDto>>> GetInstancesAsync(
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<IReadOnlyList<ParserAdminInstanceDto>>(access.Error!);

        var now = DateTime.UtcNow;
        var configuredInstances = await _dbContext.ParserInstanceConfigurations
            .AsNoTracking()
            .Include(x => x.ProxyAssignments.Where(a => a.Enabled))
            .ThenInclude(x => x.Proxy)
            .ThenInclude(x => x!.Assignment)
            .Where(x => x.Status == ParserInstanceConfigurationStatuses.Active)
            .OrderBy(x => x.ParserInstanceId)
            .ToListAsync(cancellationToken);

        var runtimeInstances = await _dbContext.ParserInstances
            .AsNoTracking()
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(cancellationToken);
        var runtimeById = runtimeInstances.ToDictionary(x => x.ParserInstanceId, StringComparer.OrdinalIgnoreCase);

        var runs = await _dbContext.ParserProxyRuns
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        var runsByInstance = runs
            .GroupBy(x => x.ParserInstanceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

        var activeLaunches = await _dbContext.ParserLaunchRequests
            .AsNoTracking()
            .Where(x => ParserLaunchRequestStatuses.Active.Contains(x.Status))
            .OrderByDescending(x => x.RequestedAtUtc)
            .ToListAsync(cancellationToken);
        var activeLaunchesByInstance = activeLaunches
            .GroupBy(x => x.ParserInstanceConfigurationId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var result = configuredInstances
            .Select(config => MapConfiguredInstance(config, runtimeById, runsByInstance, activeLaunchesByInstance, now))
            .ToList();

        var configuredIds = configuredInstances
            .Select(x => x.ParserInstanceId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        result.AddRange(runtimeInstances
            .Where(x => !configuredIds.Contains(x.ParserInstanceId))
            .Select(instance => MapRuntimeOnlyInstance(instance, runsByInstance, now)));

        return ServiceResult<IReadOnlyList<ParserAdminInstanceDto>>.Success(result);
    }

    public async Task<ServiceResult<IReadOnlyList<ParserAdminProxyRunJournalDto>>> GetJournalAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<IReadOnlyList<ParserAdminProxyRunJournalDto>>(access.Error!);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, MaxPageSize);

        var now = DateTime.UtcNow;
        var runs = await _dbContext.ParserProxyRuns
            .AsNoTracking()
            .Where(x => x.Status == ParserProxyRunStatuses.Completed ||
                        x.Status == ParserProxyRunStatuses.Failed ||
                        x.Status == ParserProxyRunStatuses.Interrupted)
            .OrderByDescending(x => x.FinishedAtUtc)
            .ThenByDescending(x => x.StartedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var runIds = runs.Select(x => x.Id).ToList();
        var productEffects = runIds.Count == 0
            ? []
            : await _dbContext.ParserRunProductEffects
                .AsNoTracking()
                .Where(x => runIds.Contains(x.ParserProxyRunId))
                .Select(x => new
                {
                    x.ParserProxyRunId,
                    x.WbProductId,
                    x.EffectType
                })
                .ToListAsync(cancellationToken);
        var productEffectCounts = productEffects
            .GroupBy(x => x.ParserProxyRunId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var createdProductIds = group
                        .Where(x => x.EffectType == ParserRunProductEffectTypes.Created)
                        .Select(x => x.WbProductId)
                        .Distinct(StringComparer.Ordinal)
                        .ToHashSet(StringComparer.Ordinal);
                    var updatedProductsCount = group
                        .Where(x => x.EffectType == ParserRunProductEffectTypes.Updated &&
                                    !createdProductIds.Contains(x.WbProductId))
                        .Select(x => x.WbProductId)
                        .Distinct(StringComparer.Ordinal)
                        .Count();
                    return new ProductEffectCounts(createdProductIds.Count, updatedProductsCount, true);
                });

        var journal = runs
            .Select(x =>
            {
                productEffectCounts.TryGetValue(x.Id, out var effectCounts);
                effectCounts ??= ProductEffectCounts.Empty;
                return new ParserAdminProxyRunJournalDto(
                    x.Id,
                    x.ParserInstanceId,
                    x.ParserCycleId,
                    x.CycleKind,
                    x.ExternalProxyRunId,
                    x.ProxyKey,
                    x.SourceCategory,
                    x.SourceSubcategory,
                    x.EgressIp,
                    x.TokenRef,
                    x.SessionStatus,
                    x.Status,
                    x.Phase,
                    x.PlannedProductsCount,
                    x.DownloadedProductsCount,
                    effectCounts.CreatedProductsCount,
                    effectCounts.UpdatedProductsCount,
                    effectCounts.HasProductEffectsLedger,
                    x.PlannedRangesCount,
                    x.CompletedRangesCount,
                    x.RangeProgressPercent,
                    x.RangeChecksCount,
                    x.FinalRangesCount,
                    x.EmptyRangesCount,
                    x.SplitRangesCount,
                    x.StartedAtUtc,
                    x.LastHeartbeatAtUtc,
                    x.FinishedAtUtc,
                    RuntimeMinutes(
                        x.StartedAtUtc,
                        x.Status == ParserProxyRunStatuses.Interrupted ? x.LastHeartbeatAtUtc : x.FinishedAtUtc,
                        now),
                    Rate(x.DownloadedProductsCount, x.StartedAtUtc, x.LastHeartbeatAtUtc),
                    Rate(x.RangeChecksCount, x.StartedAtUtc, x.LastHeartbeatAtUtc),
                    x.Error);
            })
            .ToList();

        return ServiceResult<IReadOnlyList<ParserAdminProxyRunJournalDto>>.Success(journal);
    }

    public async Task<ServiceResult<IReadOnlyList<ParserAdminBatchListItemDto>>> GetBatchesAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<IReadOnlyList<ParserAdminBatchListItemDto>>(access.Error!);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, MaxPageSize);

        var query = _dbContext.ParserBatchSubmissions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        var batches = await query
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.AcceptedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapBatch(x))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<ParserAdminBatchListItemDto>>.Success(batches);
    }

    public async Task<ServiceResult<ParserAdminBatchDetailDto>> GetBatchAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<ParserAdminBatchDetailDto>(access.Error!);

        var batch = await _dbContext.ParserBatchSubmissions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => MapBatch(x))
            .SingleOrDefaultAsync(cancellationToken);

        if (batch is null)
            return ServiceResult<ParserAdminBatchDetailDto>.NotFound("Parser batch was not found.");

        var events = await _dbContext.ParserBatchSubmissionEvents
            .AsNoTracking()
            .Where(x => x.BatchSubmissionId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new ParserAdminBatchEventDto(x.Id, x.EventType, x.Status, x.Message, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var artifacts = await _dbContext.ParserBatchArtifacts
            .AsNoTracking()
            .Where(x => x.BatchSubmissionId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new ParserAdminBatchArtifactDto(x.Id, x.ArtifactKind, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return ServiceResult<ParserAdminBatchDetailDto>.Success(new ParserAdminBatchDetailDto(batch, events, artifacts));
    }

    public async Task<ServiceResult<IReadOnlyList<ParserAdminNicheDto>>> GetNichesAsync(
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<IReadOnlyList<ParserAdminNicheDto>>(access.Error!);

        var assignments = await _dbContext.ParserNicheAssignments
            .AsNoTracking()
            .OrderBy(x => x.SourceCategory)
            .ThenBy(x => x.SourceSubcategory)
            .ToListAsync(cancellationToken);

        var batchGroups = await _dbContext.ParserBatchSubmissions
            .AsNoTracking()
            .GroupBy(x => new { x.SourceCategory, x.SourceSubcategory })
            .Select(x => new
            {
                x.Key.SourceCategory,
                x.Key.SourceSubcategory,
                LastSuccess = x.Where(batch => batch.Status == ParserBatchStatuses.Completed)
                    .Max(batch => (DateTime?)batch.CompletedAtUtc),
                FailedCount = x.Count(batch =>
                    batch.Status == ParserBatchStatuses.FailedRetryable ||
                    batch.Status == ParserBatchStatuses.FailedFinal)
            })
            .ToListAsync(cancellationToken);

        var latestByNiche = await _dbContext.ParserBatchSubmissions
            .AsNoTracking()
            .GroupBy(x => new { x.SourceCategory, x.SourceSubcategory })
            .Select(x => x.OrderByDescending(batch => batch.AcceptedAtUtc).ThenByDescending(batch => batch.Id).First())
            .ToListAsync(cancellationToken);

        var groupByKey = batchGroups.ToDictionary(
            x => NicheKey(x.SourceCategory, x.SourceSubcategory),
            StringComparer.OrdinalIgnoreCase);
        var latestByKey = latestByNiche.ToDictionary(
            x => NicheKey(x.SourceCategory, x.SourceSubcategory),
            StringComparer.OrdinalIgnoreCase);
        var assignmentByKey = assignments
            .GroupBy(x => NicheKey(x.SourceCategory, x.SourceSubcategory), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(item => item.UpdatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var keys = groupByKey.Keys
            .Union(assignmentByKey.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var now = DateTime.UtcNow;
        var result = keys.Select(key =>
        {
            groupByKey.TryGetValue(key, out var group);
            assignmentByKey.TryGetValue(key, out var assignment);
            latestByKey.TryGetValue(key, out var latest);

            var category = group?.SourceCategory ?? assignment?.SourceCategory ?? string.Empty;
            var subcategory = group?.SourceSubcategory ?? assignment?.SourceSubcategory ?? string.Empty;
            var lastSuccess = group?.LastSuccess;
            return new ParserAdminNicheDto(
                category,
                subcategory,
                assignment?.ParserInstanceId,
                latest?.ProxyKey ?? assignment?.ProxyKey,
                assignment?.IsEnabled ?? true,
                lastSuccess,
                lastSuccess.HasValue ? (now - lastSuccess.Value).TotalHours : null,
                group?.FailedCount ?? 0);
        }).ToList();

        return ServiceResult<IReadOnlyList<ParserAdminNicheDto>>.Success(result);
    }

    public async Task<ServiceResult<IReadOnlyList<ParserAdminErrorDto>>> GetErrorsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<IReadOnlyList<ParserAdminErrorDto>>(access.Error!);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, MaxPageSize);

        var errors = await (
                from batch in _dbContext.ParserBatchSubmissions.AsNoTracking()
                where (batch.Status == ParserBatchStatuses.FailedRetryable ||
                       batch.Status == ParserBatchStatuses.FailedFinal) &&
                      batch.Error != null
                orderby batch.UpdatedAtUtc descending, batch.Id descending
                select new ParserAdminErrorDto(
                    batch.Id,
                    batch.ParserInstanceId,
                    batch.ExternalBatchId,
                    batch.SourceCategory,
                    batch.SourceSubcategory,
                    batch.ProxyKey,
                    batch.Status,
                    batch.Error!,
                    batch.UpdatedAtUtc))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<ParserAdminErrorDto>>.Success(errors);
    }

    public async Task<ServiceResult<ParserAdminRetryResponse>> RetryBatchAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<ParserAdminRetryResponse>(access.Error!);

        var batch = await _dbContext.ParserBatchSubmissions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (batch is null)
            return ServiceResult<ParserAdminRetryResponse>.NotFound("Parser batch was not found.");

        if (batch.Status is not (ParserBatchStatuses.FailedRetryable or ParserBatchStatuses.FailedFinal))
            return ServiceResult<ParserAdminRetryResponse>.Conflict("Only failed parser batches can be retried.");

        var now = DateTime.UtcNow;
        batch.Retry(now);
        _dbContext.ParserBatchSubmissionEvents.Add(
            new ParserBatchSubmissionEvent(batch.Id, "retry_requested", batch.Status, null, now));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ParserAdminRetryResponse>.Success(
            new ParserAdminRetryResponse(batch.Id, batch.ExternalBatchId, batch.Status, now));
    }

    private async Task<ServiceResult> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.RoleId.HasValue)
            return ServiceResult.Unauthorized("Authentication is required.");

        var isAdmin = await _dbContext.Roles
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == _currentUser.RoleId.Value &&
                     x.Name == AdminRoleName,
                cancellationToken);

        return isAdmin
            ? ServiceResult.Success()
            : ServiceResult.Forbidden("Administrator role is required.");
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

    private static ParserAdminBatchListItemDto MapBatch(ParserBatchSubmission batch) =>
        new(
            batch.Id,
            batch.ParserInstanceId,
            batch.ExternalBatchId,
            batch.SourceCategory,
            batch.SourceSubcategory,
            batch.ProxyKey,
            batch.BatchKind,
            batch.Status,
            batch.AttemptsCount,
            batch.AcceptedAtUtc,
            batch.ProcessingStartedAtUtc,
            batch.CompletedAtUtc,
            batch.Error);

    private static ParserAdminInstanceDto MapConfiguredInstance(
        ParserInstanceConfiguration config,
        IReadOnlyDictionary<string, ParserInstance> runtimeById,
        IReadOnlyDictionary<string, List<ParserProxyRun>> runsByInstance,
        IReadOnlyDictionary<Guid, List<ParserLaunchRequest>> activeLaunchesByInstance,
        DateTime now)
    {
        runtimeById.TryGetValue(config.ParserInstanceId, out var runtimeInstance);
        runsByInstance.TryGetValue(config.ParserInstanceId, out var instanceRuns);
        activeLaunchesByInstance.TryGetValue(config.Id, out var activeLaunches);
        activeLaunches ??= [];
        instanceRuns ??= [];
        var latestRuns = LatestRuns(instanceRuns);
        var latestByProxy = latestRuns
            .GroupBy(x => x.ProxyKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(run => run.StartedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var proxies = new List<ParserAdminProxyRunDto>();
        foreach (var assignment in config.ProxyAssignments.Where(x => x.Enabled && x.Proxy is not null).OrderBy(x => x.Proxy!.Key))
        {
            var proxyKey = assignment.Proxy!.Key;
            var activeLaunch = ActiveLaunchForProxy(activeLaunches, proxyKey);
            var hasLatestRun = latestByProxy.Remove(proxyKey, out var run);
            if (hasLatestRun && run is not null && ShouldShowRunOverLaunch(run, activeLaunch))
            {
                proxies.Add(MapProxyRun(run, now));
            }
            else if (activeLaunch is not null)
            {
                proxies.Add(MapQueuedProxy(assignment, activeLaunch, now));
            }
            else if (run is not null)
            {
                proxies.Add(MapProxyRun(run, now));
            }
            else
            {
                proxies.Add(MapConfiguredProxy(assignment, config.UpdatedAtUtc));
            }
        }

        proxies.AddRange(latestByProxy.Values.OrderBy(x => x.ProxyKey).Select(x => MapProxyRun(x, now)));

        var planned = proxies.Sum(x => x.PlannedProductsCount);
        var downloaded = proxies.Sum(x => x.DownloadedProductsCount);
        var hasRuntimeRuns = proxies.Any(x => x.Status != "configured");
        var startedAt = hasRuntimeRuns
            ? proxies.Where(x => x.Status != "configured").Select(x => (DateTime?)x.StartedAtUtc).Min()
                ?? runtimeInstance?.LastSeenAtUtc
                ?? config.UpdatedAtUtc
            : config.UpdatedAtUtc;
        var finishedAt = proxies.Count > 0 &&
                         proxies.All(x => x.Status == "configured" || EffectiveRuntimeEnd(x) is not null)
            ? proxies.Select(EffectiveRuntimeEnd).Where(x => x.HasValue).Max()
            : null;

        return new ParserAdminInstanceDto(
            config.Id,
            config.ParserInstanceId,
            config.DisplayName,
            proxies.Count(IsActiveProxyState),
            config.ProxyAssignments.Count(x => x.Enabled),
            planned,
            downloaded,
            Percent(downloaded, planned),
            hasRuntimeRuns ? RuntimeMinutes(startedAt, finishedAt, now) : 0,
            runtimeInstance?.LastSeenAtUtc ?? config.UpdatedAtUtc,
            proxies);
    }

    private static ParserAdminInstanceDto MapRuntimeOnlyInstance(
        ParserInstance instance,
        IReadOnlyDictionary<string, List<ParserProxyRun>> runsByInstance,
        DateTime now)
    {
        runsByInstance.TryGetValue(instance.ParserInstanceId, out var instanceRuns);
        instanceRuns ??= [];
        var proxies = LatestRuns(instanceRuns).Select(x => MapProxyRun(x, now)).ToList();
        var planned = proxies.Sum(x => x.PlannedProductsCount);
        var downloaded = proxies.Sum(x => x.DownloadedProductsCount);
        var startedAt = proxies.Count > 0 ? proxies.Min(x => x.StartedAtUtc) : instance.LastSeenAtUtc;
        var finishedAt = proxies.Count > 0 && proxies.All(x => EffectiveRuntimeEnd(x) is not null)
            ? proxies.Select(EffectiveRuntimeEnd).Where(x => x.HasValue).Max()
            : null;

        return new ParserAdminInstanceDto(
            instance.Id,
            instance.ParserInstanceId,
            instance.DisplayName,
            proxies.Count(x => x.Status == ParserProxyRunStatuses.Running),
            proxies.Count,
            planned,
            downloaded,
            Percent(downloaded, planned),
            RuntimeMinutes(startedAt, finishedAt, now),
            instance.LastSeenAtUtc,
            proxies);
    }

    private static List<ParserProxyRun> LatestRuns(IReadOnlyList<ParserProxyRun> instanceRuns)
    {
        var latestRunKey = LatestMonitoringCycleId(instanceRuns);
        return latestRunKey is not null
            ? instanceRuns
                .Where(x => x.ParserCycleId == latestRunKey ||
                            (x.Status == ParserProxyRunStatuses.Running &&
                             x.CycleKind == ParserProxyRunCycleKinds.Production))
                .OrderBy(x => x.ProxyKey)
                .ThenBy(x => x.SourceSubcategory)
                .ToList()
            : [];
    }

    private static ParserAdminProxyRunDto MapConfiguredProxy(
        ParserInstanceProxyAssignment assignment,
        DateTime timestampUtc)
    {
        var proxy = assignment.Proxy!;
        var niche = proxy.Assignment;
        return new ParserAdminProxyRunDto(
            assignment.Id,
            $"configured:{proxy.Key}",
            string.Empty,
            "configured",
            proxy.Key,
            niche?.SourceCategory ?? string.Empty,
            niche?.SourceSubcategory ?? "Без ниши",
            proxy.Ip,
            null,
            null,
            "configured",
            "idle",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            timestampUtc,
            timestampUtc,
            null,
            null,
            0,
            0,
            0,
            niche is { Enabled: true } ? null : "Ниша не назначена или отключена.");
    }

    private static ParserAdminProxyRunDto MapQueuedProxy(
        ParserInstanceProxyAssignment assignment,
        ParserLaunchRequest launch,
        DateTime now)
    {
        var proxy = assignment.Proxy!;
        var niche = proxy.Assignment;
        var plannedProductsCount = launch.LaunchMode == ParserLaunchModes.FullAll
            ? 0
            : Math.Max(0, launch.BatchLimit ?? 0) * 100;
        return new ParserAdminProxyRunDto(
            launch.Id,
            $"launch:{launch.Id}:{proxy.Key}",
            string.Empty,
            "launch",
            proxy.Key,
            niche?.SourceCategory ?? string.Empty,
            niche?.SourceSubcategory ?? "Без ниши",
            proxy.Ip,
            null,
            null,
            ParserLaunchRequestStatuses.Queued,
            ParserLaunchRequestStatuses.Queued,
            plannedProductsCount,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            launch.RequestedAtUtc,
            launch.StartedAtUtc ?? launch.RequestedAtUtc,
            null,
            null,
            RuntimeMinutes(launch.RequestedAtUtc, null, now),
            0,
            0,
            null);
    }

    private static ParserAdminProxyRunDto MapProxyRun(
        ParserProxyRun run,
        DateTime now)
    {
        return new ParserAdminProxyRunDto(
            run.Id,
            run.ExternalProxyRunId,
            run.ParserCycleId,
            run.CycleKind,
            run.ProxyKey,
            run.SourceCategory,
            run.SourceSubcategory,
            run.EgressIp,
            run.TokenRef,
            run.SessionStatus,
            run.Status,
            run.Phase,
            run.PlannedProductsCount,
            run.DownloadedProductsCount,
            run.PlannedRangesCount,
            run.CompletedRangesCount,
            run.RangeProgressPercent,
            run.RangeChecksCount,
            run.FinalRangesCount,
            run.EmptyRangesCount,
            run.SplitRangesCount,
            Percent(run.DownloadedProductsCount, run.PlannedProductsCount),
            run.StartedAtUtc,
            run.LastHeartbeatAtUtc,
            run.LastHeartbeatAtUtc,
            run.FinishedAtUtc,
            RuntimeMinutes(run.StartedAtUtc, EffectiveRuntimeEnd(run), now),
            Rate(run.DownloadedProductsCount, run.StartedAtUtc, run.LastHeartbeatAtUtc),
            Rate(run.RangeChecksCount, run.StartedAtUtc, run.LastHeartbeatAtUtc),
            run.Error);
    }

    private static double Percent(int downloaded, int planned)
    {
        if (planned <= 0)
            return 0;
        return Math.Round(Math.Clamp(downloaded * 100.0 / planned, 0, 100), 1);
    }

    private static int RuntimeMinutes(DateTime startedAtUtc, DateTime? finishedAtUtc, DateTime nowUtc)
    {
        var end = finishedAtUtc ?? nowUtc;
        if (end < startedAtUtc)
            return 0;
        return Math.Max(0, (int)Math.Round((end - startedAtUtc).TotalMinutes, MidpointRounding.AwayFromZero));
    }

    private static DateTime? EffectiveRuntimeEnd(ParserProxyRun run)
    {
        return run.Status == ParserProxyRunStatuses.Interrupted
            ? run.LastHeartbeatAtUtc
            : run.FinishedAtUtc;
    }

    private static DateTime? EffectiveRuntimeEnd(ParserAdminProxyRunDto run)
    {
        return run.Status == ParserProxyRunStatuses.Interrupted
            ? run.LastHeartbeatAtUtc
            : run.FinishedAtUtc;
    }

    private static ParserLaunchRequest? ActiveLaunchForProxy(
        IReadOnlyList<ParserLaunchRequest> activeLaunches,
        string proxyKey)
    {
        return activeLaunches.FirstOrDefault(x =>
            x.LaunchMode == ParserLaunchModes.CheckProxy
                ? string.Equals(x.ProxyKey, proxyKey, StringComparison.OrdinalIgnoreCase)
                : true);
    }

    private static bool IsActiveProxyState(ParserAdminProxyRunDto proxy)
    {
        return proxy.Status is ParserProxyRunStatuses.Running or ParserLaunchRequestStatuses.Queued;
    }

    private static bool ShouldShowRunOverLaunch(ParserProxyRun run, ParserLaunchRequest? activeLaunch)
    {
        if (activeLaunch is null)
            return true;

        if (run.Status == ParserProxyRunStatuses.Running)
            return true;

        return string.Equals(run.ParserCycleId, activeLaunch.ParserCycleId, StringComparison.OrdinalIgnoreCase);
    }

    private static double Rate(int count, DateTime startedAtUtc, DateTime? lastActivityAtUtc)
    {
        if (count <= 0 || !lastActivityAtUtc.HasValue || lastActivityAtUtc.Value <= startedAtUtc)
            return 0;

        return Math.Round(count / Math.Max(1, (lastActivityAtUtc.Value - startedAtUtc).TotalSeconds), 2);
    }

    private static string NicheKey(string sourceCategory, string sourceSubcategory) =>
        $"{sourceCategory}\u001f{sourceSubcategory}";

    private static string? LatestMonitoringCycleId(IReadOnlyList<ParserProxyRun> runs)
    {
        if (runs.Count == 0)
            return null;

        return runs.OrderByDescending(x => x.StartedAtUtc).First().ParserCycleId;
    }

    private sealed record ProductEffectCounts(
        int CreatedProductsCount,
        int UpdatedProductsCount,
        bool HasProductEffectsLedger)
    {
        public static ProductEffectCounts Empty { get; } = new(0, 0, false);
    }

}
