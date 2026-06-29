using System.Diagnostics;
using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class PublicProductAvailabilityRefreshService : IPublicProductAvailabilityRefreshService
{
    private const int WorkerPageSize = 200;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;
    private readonly ParserProductReadService _productReadService;

    public PublicProductAvailabilityRefreshService(
        ApplicationDbContext dbContext,
        ParserProductReadService productReadService)
    {
        _dbContext = dbContext;
        _productReadService = productReadService;
    }

    public async Task<ServiceResult<PublicProductAvailabilityRefreshResult>> RefreshAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var firstResult = await _productReadService.GetListAsync(BuildQuery(1), cancellationToken);
        if (!firstResult.IsSuccess)
            return ServiceResult<PublicProductAvailabilityRefreshResult>.Unavailable(firstResult.Error!.Message);

        var first = firstResult.Value!;
        var items = first.Items.ToList();
        var pages = (int)Math.Ceiling(first.TotalCount / (double)WorkerPageSize);
        for (var page = 2; page <= pages; page++)
        {
            var result = await _productReadService.GetListAsync(BuildQuery(page), cancellationToken);
            if (!result.IsSuccess)
                return ServiceResult<PublicProductAvailabilityRefreshResult>.Unavailable(result.Error!.Message);
            items.AddRange(result.Value!.Items);
        }

        stopwatch.Stop();
        var nowUtc = DateTime.UtcNow;
        var snapshot = await _dbContext.PublicProductAvailabilitySnapshots
            .FirstOrDefaultAsync(x => x.SnapshotKey == PublicProductAvailabilitySnapshot.SnapshotKeyValue, cancellationToken)
            ?? new PublicProductAvailabilitySnapshot(nowUtc);

        if (_dbContext.Entry(snapshot).State == EntityState.Detached)
            _dbContext.PublicProductAvailabilitySnapshots.Add(snapshot);

        snapshot.MarkCompleted(
            JsonSerializer.Serialize(items, JsonOptions),
            items.Count,
            nowUtc,
            (long)stopwatch.Elapsed.TotalMilliseconds);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PublicProductAvailabilityRefreshResult>.Success(
            new PublicProductAvailabilityRefreshResult(items.Count, nowUtc));
    }

    private static ParserProductListQuery BuildQuery(int page) =>
        new()
        {
            Page = page,
            PageSize = WorkerPageSize,
            Sort = "-parsedAtUtc",
            RequireDeliveryProfile = true
        };
}
