using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserProxyRunService : IParserProxyRunService
{
    private readonly ApplicationDbContext _dbContext;

    public ParserProxyRunService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<ParserProxyRunResponse>> StartAsync(
        ParserProxyRunStartRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateStart(request);
        if (validationError is not null)
            return ServiceResult<ParserProxyRunResponse>.BadRequest(validationError);

        var now = DateTime.UtcNow;
        var parserInstanceId = request.ParserInstanceId.Trim();
        var proxyKey = request.ProxyKey.Trim();
        await UpsertParserInstanceAsync(parserInstanceId, now, cancellationToken);

        var externalProxyRunId = request.ExternalProxyRunId.Trim();
        var run = await _dbContext.ParserProxyRuns
            .FirstOrDefaultAsync(
                x => x.ParserInstanceId == parserInstanceId &&
                     x.ExternalProxyRunId == externalProxyRunId,
                cancellationToken);

        var isNewRun = run is null;
        if (isNewRun)
        {
            var existingRunningRun = await _dbContext.ParserProxyRuns
                .AsNoTracking()
                .Where(x => x.ParserInstanceId == parserInstanceId &&
                            x.ProxyKey == proxyKey &&
                            x.Status == ParserProxyRunStatuses.Running)
                .OrderByDescending(x => x.StartedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingRunningRun is not null)
            {
                return ServiceResult<ParserProxyRunResponse>.Conflict(
                    $"Parser proxy '{proxyKey}' already has running process '{existingRunningRun.ExternalProxyRunId}'.");
            }

            run = new ParserProxyRun(
                parserInstanceId,
                externalProxyRunId,
                request.ParserCycleId,
                request.CycleKind,
                request.ProxyKey,
                request.SourceCategory,
                request.SourceSubcategory,
                request.PlannedProductsCount,
                request.DownloadedProductsCount,
                request.EgressIp,
                request.TokenRef,
                request.SessionStatus,
                now,
                request.Phase,
                request.PlannedRangesCount ?? 0,
                request.CompletedRangesCount ?? 0,
                request.RangeProgressPercent ?? 0,
                request.RangeChecksCount ?? 0,
                request.FinalRangesCount ?? 0,
                request.EmptyRangesCount ?? 0,
                request.SplitRangesCount ?? 0);
            _dbContext.ParserProxyRuns.Add(run);
        }
        else
        {
            run!.UpdateSession(request.EgressIp, request.TokenRef, request.SessionStatus, now);
            run.UpdateProgress(
                request.PlannedProductsCount,
                request.DownloadedProductsCount,
                now,
                request.Phase,
                request.PlannedRangesCount,
                request.CompletedRangesCount,
                request.RangeProgressPercent,
                request.RangeChecksCount,
                request.FinalRangesCount,
                request.EmptyRangesCount,
                request.SplitRangesCount);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (isNewRun && IsRunningProxyUniqueViolation(exception))
        {
            return ServiceResult<ParserProxyRunResponse>.Conflict(
                $"Parser proxy '{proxyKey}' already has running process.");
        }

        return ServiceResult<ParserProxyRunResponse>.Success(Map(run!));
    }

    public async Task<ServiceResult<ParserProxyRunResponse>> UpdateProgressAsync(
        string externalProxyRunId,
        ParserProxyRunProgressRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateProgress(externalProxyRunId, request);
        if (validationError is not null)
            return ServiceResult<ParserProxyRunResponse>.BadRequest(validationError);

        var run = await FindRunAsync(request.ParserInstanceId, externalProxyRunId, cancellationToken);
        if (run is null)
            return ServiceResult<ParserProxyRunResponse>.NotFound("Parser proxy run was not found.");

        var now = DateTime.UtcNow;
        await UpsertParserInstanceAsync(request.ParserInstanceId.Trim(), now, cancellationToken);
        run.UpdateProgress(
            request.PlannedProductsCount,
            request.DownloadedProductsCount,
            now,
            request.Phase,
            request.PlannedRangesCount,
            request.CompletedRangesCount,
            request.RangeProgressPercent,
            request.RangeChecksCount,
            request.FinalRangesCount,
            request.EmptyRangesCount,
            request.SplitRangesCount);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ParserProxyRunResponse>.Success(Map(run));
    }

    public async Task<ServiceResult<ParserProxyRunResponse>> FinishAsync(
        string externalProxyRunId,
        ParserProxyRunFinishRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateFinish(externalProxyRunId, request);
        if (validationError is not null)
            return ServiceResult<ParserProxyRunResponse>.BadRequest(validationError);

        var run = await FindRunAsync(request.ParserInstanceId, externalProxyRunId, cancellationToken);
        if (run is null)
            return ServiceResult<ParserProxyRunResponse>.NotFound("Parser proxy run was not found.");

        var now = DateTime.UtcNow;
        await UpsertParserInstanceAsync(request.ParserInstanceId.Trim(), now, cancellationToken);
        if (request.Status == ParserProxyRunStatuses.Completed)
        {
            run.Complete(request.PlannedProductsCount, request.DownloadedProductsCount, now);
        }
        else
        {
            run.Fail(request.PlannedProductsCount, request.DownloadedProductsCount, request.Error, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ParserProxyRunResponse>.Success(Map(run));
    }

    private async Task<ParserProxyRun?> FindRunAsync(
        string parserInstanceId,
        string externalProxyRunId,
        CancellationToken cancellationToken)
    {
        var normalizedParserInstanceId = parserInstanceId.Trim();
        var normalizedExternalProxyRunId = externalProxyRunId.Trim();
        return await _dbContext.ParserProxyRuns.FirstOrDefaultAsync(
            x => x.ParserInstanceId == normalizedParserInstanceId &&
                 x.ExternalProxyRunId == normalizedExternalProxyRunId,
            cancellationToken);
    }

    private async Task UpsertParserInstanceAsync(
        string parserInstanceId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var instance = await _dbContext.ParserInstances
            .FirstOrDefaultAsync(x => x.ParserInstanceId == parserInstanceId, cancellationToken);

        if (instance is null)
            _dbContext.ParserInstances.Add(new ParserInstance(parserInstanceId, parserInstanceId, now));
        else
            instance.Touch(now);
    }

    private static string? ValidateStart(ParserProxyRunStartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ParserInstanceId))
            return "Parser instance id is required.";
        if (string.IsNullOrWhiteSpace(request.ExternalProxyRunId))
            return "External proxy run id is required.";
        if (string.IsNullOrWhiteSpace(request.ProxyKey))
            return "Proxy key is required.";
        if (string.IsNullOrWhiteSpace(request.SourceCategory))
            return "Source category is required.";
        if (string.IsNullOrWhiteSpace(request.SourceSubcategory))
            return "Source subcategory is required.";
        return null;
    }

    private static string? ValidateProgress(string externalProxyRunId, ParserProxyRunProgressRequest request)
    {
        if (string.IsNullOrWhiteSpace(externalProxyRunId))
            return "External proxy run id is required.";
        if (string.IsNullOrWhiteSpace(request.ParserInstanceId))
            return "Parser instance id is required.";
        return null;
    }

    private static string? ValidateFinish(string externalProxyRunId, ParserProxyRunFinishRequest request)
    {
        var progressError = ValidateProgress(
            externalProxyRunId,
            new ParserProxyRunProgressRequest(
                request.ParserInstanceId,
                request.PlannedProductsCount,
                request.DownloadedProductsCount));
        if (progressError is not null)
            return progressError;

        if (request.Status is not (ParserProxyRunStatuses.Completed or ParserProxyRunStatuses.Failed))
            return "Parser proxy run finish status must be completed or failed.";
        return null;
    }

    private static ParserProxyRunResponse Map(ParserProxyRun run) =>
        new(
            run.Id,
            run.ParserInstanceId,
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
            run.StartedAtUtc,
            run.LastHeartbeatAtUtc,
            run.FinishedAtUtc,
            run.Error);

    private static bool IsRunningProxyUniqueViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("IX_ParserProxyRuns_parser_instance_id_proxy_key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("ParserProxyRuns_parser_instance_id_proxy_key", StringComparison.OrdinalIgnoreCase);
    }
}
