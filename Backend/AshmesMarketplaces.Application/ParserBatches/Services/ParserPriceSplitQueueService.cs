using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserPriceSplitQueueService : IParserPriceSplitQueueService
{
    private const int MaxClaimLimit = 100;

    private readonly ApplicationDbContext _dbContext;

    public ParserPriceSplitQueueService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<ParserPriceSplitJobResponse>> EnsureJobAsync(
        ParserPriceSplitEnsureJobRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateJobRequest(request);
        if (validationError is not null)
            return ServiceResult<ParserPriceSplitJobResponse>.BadRequest(validationError);

        var now = DateTime.UtcNow;
        var parserInstanceId = request.ParserInstanceId.Trim();
        var proxyKey = request.ProxyKey.Trim();
        var category = request.SourceCategory.Trim();
        var subcategory = request.SourceSubcategory.Trim();

        var job = await _dbContext.ParserPriceSplitJobs
            .FirstOrDefaultAsync(
                x => x.ParserInstanceId == parserInstanceId &&
                     x.ProxyKey == proxyKey &&
                     x.SourceCategory == category &&
                     x.SourceSubcategory == subcategory &&
                     x.Status == ParserPriceSplitJobStatuses.Active,
                cancellationToken);

        if (job is null)
        {
            job = new ParserPriceSplitJob(
                parserInstanceId,
                proxyKey,
                category,
                subcategory,
                request.MinPriceU,
                request.MaxPriceU,
                now);
            _dbContext.ParserPriceSplitJobs.Add(job);
            _dbContext.ParserPriceSplitRanges.Add(
                new ParserPriceSplitRange(job.Id, null, request.MinPriceU, request.MaxPriceU, null, now));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await RefreshCountersAsync(job, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ParserPriceSplitJobResponse>.Success(MapJob(job));
    }

    public async Task<ServiceResult<ParserPriceSplitJobResponse>> GetCurrentJobAsync(
        ParserPriceSplitCurrentJobRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCurrentJobRequest(request);
        if (validationError is not null)
            return ServiceResult<ParserPriceSplitJobResponse>.BadRequest(validationError);

        var parserInstanceId = request.ParserInstanceId.Trim();
        var proxyKey = request.ProxyKey.Trim();
        var category = request.SourceCategory.Trim();
        var subcategory = request.SourceSubcategory.Trim();

        var job = await _dbContext.ParserPriceSplitJobs
            .FirstOrDefaultAsync(
                x => x.ParserInstanceId == parserInstanceId &&
                     x.ProxyKey == proxyKey &&
                     x.SourceCategory == category &&
                     x.SourceSubcategory == subcategory &&
                     x.Status == ParserPriceSplitJobStatuses.Active,
                cancellationToken);

        if (job is null)
            return ServiceResult<ParserPriceSplitJobResponse>.NotFound("Active price split job was not found.");

        var now = DateTime.UtcNow;
        await RefreshCountersAsync(job, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ParserPriceSplitJobResponse>.Success(MapJob(job));
    }

    public async Task<ServiceResult<ParserPriceSplitClaimRangesResponse>> ClaimRangesAsync(
        ParserPriceSplitClaimRangesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.JobId == Guid.Empty)
            return ServiceResult<ParserPriceSplitClaimRangesResponse>.BadRequest("Price split job id is required.");
        if (string.IsNullOrWhiteSpace(request.ParserInstanceId))
            return ServiceResult<ParserPriceSplitClaimRangesResponse>.BadRequest("Parser instance id is required.");
        if (string.IsNullOrWhiteSpace(request.ProxyKey))
            return ServiceResult<ParserPriceSplitClaimRangesResponse>.BadRequest("Proxy key is required.");

        var now = DateTime.UtcNow;
        var limit = Math.Clamp(request.Limit <= 0 ? 10 : request.Limit, 1, MaxClaimLimit);
        var parserInstanceId = request.ParserInstanceId.Trim();
        var proxyKey = request.ProxyKey.Trim();

        var job = await _dbContext.ParserPriceSplitJobs
            .FirstOrDefaultAsync(
                x => x.Id == request.JobId &&
                     x.ParserInstanceId == parserInstanceId &&
                     x.ProxyKey == proxyKey,
                cancellationToken);
        if (job is null)
            return ServiceResult<ParserPriceSplitClaimRangesResponse>.NotFound("Price split job was not found.");

        var ranges = await _dbContext.ParserPriceSplitRanges
            .Where(x => x.JobId == request.JobId &&
                        (x.Status == ParserPriceSplitRangeStatuses.Pending ||
                         x.Status == ParserPriceSplitRangeStatuses.FailedRetryable ||
                         (x.Status == ParserPriceSplitRangeStatuses.Cooldown &&
                          x.CooldownUntilUtc != null &&
                          x.CooldownUntilUtc <= now)))
            .OrderBy(x => x.MinPriceU)
            .ThenBy(x => x.MaxPriceU)
            .Take(limit)
            .ToListAsync(cancellationToken);

        foreach (var range in ranges)
            range.Claim(now);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ParserPriceSplitClaimRangesResponse>.Success(
            new ParserPriceSplitClaimRangesResponse(ranges.Select(MapRange).ToList()));
    }

    public async Task<ServiceResult<ParserPriceSplitRangeResponse>> UpdateRangeAsync(
        Guid rangeId,
        ParserPriceSplitUpdateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var range = await FindRangeForMutationAsync(rangeId, request.ParserInstanceId, request.ProxyKey, cancellationToken);
        if (range is null)
            return ServiceResult<ParserPriceSplitRangeResponse>.NotFound("Price split range was not found.");

        var now = DateTime.UtcNow;
        switch (request.Status)
        {
            case ParserPriceSplitRangeStatuses.Pending:
                range.MarkPending(
                    request.ExpectedTotal,
                    request.NextPage ?? range.NextPage,
                    request.NextItemOffset ?? range.NextItemOffset,
                    now);
                break;
            case ParserPriceSplitRangeStatuses.Completed:
                range.MarkCompleted(request.ExpectedTotal, now);
                break;
            case ParserPriceSplitRangeStatuses.Cooldown:
                range.MarkCooldown(
                    request.Error,
                    request.CooldownUntilUtc?.ToUniversalTime() ?? now.AddMinutes(5),
                    now);
                break;
            case ParserPriceSplitRangeStatuses.FailedRetryable:
                range.MarkFailedRetryable(request.Error, now);
                break;
            case ParserPriceSplitRangeStatuses.FailedFinal:
                range.MarkFailedFinal(request.Error, now);
                break;
            default:
                return ServiceResult<ParserPriceSplitRangeResponse>.BadRequest("Unsupported price split range status.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ParserPriceSplitRangeResponse>.Success(MapRange(range));
    }

    public async Task<ServiceResult<ParserPriceSplitRangeResponse>> SplitRangeAsync(
        Guid rangeId,
        ParserPriceSplitSplitRangeRequest request,
        CancellationToken cancellationToken)
    {
        var range = await FindRangeForMutationAsync(rangeId, request.ParserInstanceId, request.ProxyKey, cancellationToken);
        if (range is null)
            return ServiceResult<ParserPriceSplitRangeResponse>.NotFound("Price split range was not found.");
        if (request.Children.Count == 0)
            return ServiceResult<ParserPriceSplitRangeResponse>.BadRequest("At least one child range is required.");

        var now = DateTime.UtcNow;
        foreach (var child in request.Children)
        {
            if (child.MinPriceU > child.MaxPriceU)
                return ServiceResult<ParserPriceSplitRangeResponse>.BadRequest("Child range min price must not exceed max price.");

            var exists = await _dbContext.ParserPriceSplitRanges.AnyAsync(
                x => x.JobId == range.JobId &&
                     x.ParentRangeId == range.Id &&
                     x.MinPriceU == child.MinPriceU &&
                     x.MaxPriceU == child.MaxPriceU,
                cancellationToken);
            if (!exists)
            {
                _dbContext.ParserPriceSplitRanges.Add(
                    new ParserPriceSplitRange(range.JobId, range.Id, child.MinPriceU, child.MaxPriceU, child.ExpectedTotal, now));
            }
        }

        range.MarkCompletedSplit(now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ParserPriceSplitRangeResponse>.Success(MapRange(range));
    }

    private async Task<ParserPriceSplitRange?> FindRangeForMutationAsync(
        Guid rangeId,
        string parserInstanceId,
        string proxyKey,
        CancellationToken cancellationToken)
    {
        if (rangeId == Guid.Empty || string.IsNullOrWhiteSpace(parserInstanceId) || string.IsNullOrWhiteSpace(proxyKey))
            return null;

        var normalizedParserInstanceId = parserInstanceId.Trim();
        var normalizedProxyKey = proxyKey.Trim();
        return await (
                from range in _dbContext.ParserPriceSplitRanges
                join job in _dbContext.ParserPriceSplitJobs on range.JobId equals job.Id
                where range.Id == rangeId &&
                      job.ParserInstanceId == normalizedParserInstanceId &&
                      job.ProxyKey == normalizedProxyKey
                select range)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task RefreshCountersAsync(ParserPriceSplitJob job, DateTime now, CancellationToken cancellationToken)
    {
        var total = await _dbContext.ParserPriceSplitRanges.CountAsync(x => x.JobId == job.Id, cancellationToken);
        var completed = await _dbContext.ParserPriceSplitRanges.CountAsync(
            x => x.JobId == job.Id &&
                 (x.Status == ParserPriceSplitRangeStatuses.Completed ||
                  x.Status == ParserPriceSplitRangeStatuses.CompletedSplit),
            cancellationToken);
        job.UpdateCounters(total, completed, now);
    }

    private static string? ValidateJobRequest(ParserPriceSplitEnsureJobRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ParserInstanceId))
            return "Parser instance id is required.";
        if (string.IsNullOrWhiteSpace(request.ProxyKey))
            return "Proxy key is required.";
        if (string.IsNullOrWhiteSpace(request.SourceCategory))
            return "Source category is required.";
        if (string.IsNullOrWhiteSpace(request.SourceSubcategory))
            return "Source subcategory is required.";
        if (request.MinPriceU > request.MaxPriceU)
            return "Min price must not exceed max price.";
        return null;
    }

    private static string? ValidateCurrentJobRequest(ParserPriceSplitCurrentJobRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ParserInstanceId))
            return "Parser instance id is required.";
        if (string.IsNullOrWhiteSpace(request.ProxyKey))
            return "Proxy key is required.";
        if (string.IsNullOrWhiteSpace(request.SourceCategory))
            return "Source category is required.";
        if (string.IsNullOrWhiteSpace(request.SourceSubcategory))
            return "Source subcategory is required.";
        return null;
    }

    private static ParserPriceSplitJobResponse MapJob(ParserPriceSplitJob job) =>
        new(
            job.Id,
            job.ParserInstanceId,
            job.ProxyKey,
            job.SourceCategory,
            job.SourceSubcategory,
            job.Status,
            job.MinPriceU,
            job.MaxPriceU,
            job.TotalRangesCount,
            job.CompletedRangesCount);

    private static ParserPriceSplitRangeResponse MapRange(ParserPriceSplitRange range) =>
        new(
            range.Id,
            range.JobId,
            range.ParentRangeId,
            range.MinPriceU,
            range.MaxPriceU,
            range.ExpectedTotal,
            range.NextPage,
            range.NextItemOffset,
            range.Status,
            range.AttemptsCount,
            range.CooldownUntilUtc,
            range.Error);
}
