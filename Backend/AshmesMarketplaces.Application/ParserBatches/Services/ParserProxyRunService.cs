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
        await UpsertParserInstanceAsync(parserInstanceId, now, cancellationToken);

        var externalProxyRunId = request.ExternalProxyRunId.Trim();
        var run = await _dbContext.ParserProxyRuns
            .FirstOrDefaultAsync(
                x => x.ParserInstanceId == parserInstanceId &&
                     x.ExternalProxyRunId == externalProxyRunId,
                cancellationToken);

        if (run is null)
        {
            run = new ParserProxyRun(
                parserInstanceId,
                externalProxyRunId,
                request.ProxyKey,
                request.SourceCategory,
                request.SourceSubcategory,
                request.PlannedProductsCount,
                request.DownloadedProductsCount,
                now);
            _dbContext.ParserProxyRuns.Add(run);
        }
        else
        {
            run.UpdateProgress(request.PlannedProductsCount, request.DownloadedProductsCount, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ParserProxyRunResponse>.Success(Map(run));
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
        run.UpdateProgress(request.PlannedProductsCount, request.DownloadedProductsCount, now);
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
            run.ProxyKey,
            run.SourceCategory,
            run.SourceSubcategory,
            run.Status,
            run.PlannedProductsCount,
            run.DownloadedProductsCount,
            run.StartedAtUtc,
            run.LastHeartbeatAtUtc,
            run.FinishedAtUtc,
            run.Error);
}
