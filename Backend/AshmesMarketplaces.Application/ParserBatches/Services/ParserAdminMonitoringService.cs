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
        var instances = await _dbContext.ParserInstances
            .AsNoTracking()
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(cancellationToken);

        var runs = await _dbContext.ParserProxyRuns
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var runsByInstance = runs
            .GroupBy(x => x.ParserInstanceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

        var result = instances
            .Select(instance =>
            {
                runsByInstance.TryGetValue(instance.ParserInstanceId, out var instanceRuns);
                instanceRuns ??= [];
                var latestStartedAt = instanceRuns.Count > 0
                    ? instanceRuns.Max(x => x.StartedAtUtc)
                    : (DateTime?)null;
                var latestRuns = latestStartedAt.HasValue
                    ? instanceRuns
                        .Where(x => x.StartedAtUtc >= latestStartedAt.Value.AddMinutes(-5) ||
                                    x.Status == ParserProxyRunStatuses.Running)
                        .OrderBy(x => x.ProxyKey)
                        .ThenBy(x => x.SourceSubcategory)
                        .ToList()
                    : [];
                var proxies = latestRuns.Select(x => MapProxyRun(x, now)).ToList();
                var planned = proxies.Sum(x => x.PlannedProductsCount);
                var downloaded = proxies.Sum(x => x.DownloadedProductsCount);
                var startedAt = proxies.Count > 0 ? proxies.Min(x => x.StartedAtUtc) : instance.LastSeenAtUtc;
                var finishedAt = proxies.Count > 0 && proxies.All(x => x.FinishedAtUtc.HasValue)
                    ? proxies.Max(x => x.FinishedAtUtc)
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
            })
            .ToList();

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
        var journal = await _dbContext.ParserProxyRuns
            .AsNoTracking()
            .Where(x => x.Status == ParserProxyRunStatuses.Completed ||
                        x.Status == ParserProxyRunStatuses.Failed)
            .OrderByDescending(x => x.FinishedAtUtc)
            .ThenByDescending(x => x.StartedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ParserAdminProxyRunJournalDto(
                x.Id,
                x.ParserInstanceId,
                x.ExternalProxyRunId,
                x.ProxyKey,
                x.SourceCategory,
                x.SourceSubcategory,
                x.Status,
                x.PlannedProductsCount,
                x.DownloadedProductsCount,
                x.StartedAtUtc,
                x.FinishedAtUtc,
                RuntimeMinutes(x.StartedAtUtc, x.FinishedAtUtc, now),
                x.Error))
            .ToListAsync(cancellationToken);

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

    private static ParserAdminProxyRunDto MapProxyRun(ParserProxyRun run, DateTime now)
    {
        return new ParserAdminProxyRunDto(
            run.Id,
            run.ExternalProxyRunId,
            run.ProxyKey,
            run.SourceCategory,
            run.SourceSubcategory,
            run.Status,
            run.PlannedProductsCount,
            run.DownloadedProductsCount,
            Percent(run.DownloadedProductsCount, run.PlannedProductsCount),
            run.StartedAtUtc,
            run.LastHeartbeatAtUtc,
            run.FinishedAtUtc,
            RuntimeMinutes(run.StartedAtUtc, run.FinishedAtUtc, now),
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

    private static string NicheKey(string sourceCategory, string sourceSubcategory) =>
        $"{sourceCategory}\u001f{sourceSubcategory}";
}
