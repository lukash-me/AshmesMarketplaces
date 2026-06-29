using System.Diagnostics;
using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public sealed class PublicMarketIntelligenceRefreshService : IPublicMarketIntelligenceRefreshService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;
    private readonly IPublicMarketIntelligenceReadService _liveReadService;

    public PublicMarketIntelligenceRefreshService(
        ApplicationDbContext dbContext,
        IPublicMarketIntelligenceReadService liveReadService)
    {
        _dbContext = dbContext;
        _liveReadService = liveReadService;
    }

    public async Task<ServiceResult<IReadOnlyList<PublicMarketIntelligenceDto>>> RefreshAllAsync(CancellationToken cancellationToken)
    {
        var snapshots = new List<PublicMarketIntelligenceDto>();
        var errors = new List<string>();

        foreach (var context in PublicMarketIntelligenceContextCatalog.All)
        {
            var result = await RefreshAsync(context, cancellationToken);
            if (result.IsSuccess)
            {
                snapshots.Add(result.Value!);
            }
            else
            {
                errors.Add($"{context.SourceSubcategory}: {result.Error!.Message}");
            }
        }

        return errors.Count == 0
            ? ServiceResult<IReadOnlyList<PublicMarketIntelligenceDto>>.Success(snapshots)
            : ServiceResult<IReadOnlyList<PublicMarketIntelligenceDto>>.Unavailable(string.Join("; ", errors));
    }

    public async Task<ServiceResult<PublicMarketIntelligenceDto>> RefreshAsync(
        PublicMarketIntelligenceQuery query,
        CancellationToken cancellationToken)
    {
        var context = PublicMarketIntelligenceContextCatalog.Resolve(query);
        if (context is null)
            return ServiceResult<PublicMarketIntelligenceDto>.BadRequest("Ниша маркетинговой разведки не поддерживается.");

        var stopwatch = Stopwatch.StartNew();
        var liveResult = await _liveReadService.GetAsync(context, cancellationToken);
        stopwatch.Stop();

        if (!liveResult.IsSuccess)
        {
            await MarkFailedAsync(context, liveResult.Error!.Message, cancellationToken);
            return ServiceResult<PublicMarketIntelligenceDto>.Unavailable(liveResult.Error.Message);
        }

        var live = liveResult.Value!;
        var calculatedAtUtc = DateTime.UtcNow;
        var snapshot = await LoadSnapshotAsync(context, cancellationToken)
            ?? new PublicMarketIntelligenceSnapshot(
                context.SourceCategory!,
                context.SourceSubcategory!,
                context.Query!,
                context.SourceRegionDest!,
                context.Sort!,
                context.TopN,
                calculatedAtUtc);

        if (_dbContext.Entry(snapshot).State == EntityState.Detached)
            _dbContext.PublicMarketIntelligenceSnapshots.Add(snapshot);

        snapshot.MarkCompleted(
            JsonSerializer.Serialize(live, JsonOptions),
            live.PriceQualityMap.Summary.TotalPoints,
            live.ObservationWindow.LatestObservedAtUtc,
            calculatedAtUtc,
            (long)stopwatch.Elapsed.TotalMilliseconds);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PublicMarketIntelligenceDto>.Success(live);
    }

    private async Task MarkFailedAsync(
        PublicMarketIntelligenceQuery context,
        string error,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadSnapshotAsync(context, cancellationToken);
        if (snapshot is null)
        {
            var nowUtc = DateTime.UtcNow;
            snapshot = new PublicMarketIntelligenceSnapshot(
                context.SourceCategory!,
                context.SourceSubcategory!,
                context.Query!,
                context.SourceRegionDest!,
                context.Sort!,
                context.TopN,
                nowUtc);
            _dbContext.PublicMarketIntelligenceSnapshots.Add(snapshot);
        }

        snapshot.MarkFailed(DateTime.UtcNow, Truncate(error));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<PublicMarketIntelligenceSnapshot?> LoadSnapshotAsync(
        PublicMarketIntelligenceQuery context,
        CancellationToken cancellationToken) =>
        _dbContext.PublicMarketIntelligenceSnapshots
            .FirstOrDefaultAsync(x =>
                x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.Query == context.Query
                && x.SourceRegionDest == context.SourceRegionDest
                && x.Sort == context.Sort
                && x.TopN == context.TopN,
                cancellationToken);

    private static string Truncate(string value)
    {
        const int maxLength = 4000;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
