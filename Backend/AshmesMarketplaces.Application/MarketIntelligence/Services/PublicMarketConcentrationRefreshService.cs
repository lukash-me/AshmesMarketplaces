using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public sealed class PublicMarketConcentrationRefreshService : IPublicMarketConcentrationRefreshService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;
    private readonly IPublicMarketIntelligenceReadService _liveReadService;

    public PublicMarketConcentrationRefreshService(
        ApplicationDbContext dbContext,
        IPublicMarketIntelligenceReadService liveReadService)
    {
        _dbContext = dbContext;
        _liveReadService = liveReadService;
    }

    public async Task<ServiceResult<IReadOnlyList<PublicMarketConcentrationSnapshotDto>>> RefreshAllAsync(CancellationToken cancellationToken)
    {
        var snapshots = new List<PublicMarketConcentrationSnapshotDto>();
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
            ? ServiceResult<IReadOnlyList<PublicMarketConcentrationSnapshotDto>>.Success(snapshots)
            : ServiceResult<IReadOnlyList<PublicMarketConcentrationSnapshotDto>>.Unavailable(string.Join("; ", errors));
    }

    public async Task<ServiceResult<PublicMarketConcentrationSnapshotDto>> RefreshAsync(
        PublicMarketIntelligenceQuery query,
        CancellationToken cancellationToken)
    {
        var context = PublicMarketIntelligenceContextCatalog.Resolve(query);
        if (context is null)
            return ServiceResult<PublicMarketConcentrationSnapshotDto>.BadRequest("Ниша для расчета концентрации рынка не поддерживается.");

        var stopwatch = Stopwatch.StartNew();
        var liveResult = await _liveReadService.GetAsync(context, cancellationToken);
        stopwatch.Stop();

        if (!liveResult.IsSuccess)
        {
            await MarkFailedAsync(context, liveResult.Error!.Message, cancellationToken);
            return ServiceResult<PublicMarketConcentrationSnapshotDto>.Unavailable(liveResult.Error.Message);
        }

        var live = liveResult.Value!;
        var calculatedAtUtc = DateTime.UtcNow;
        var snapshot = await LoadSnapshotAsync(context, cancellationToken)
            ?? new PublicMarketConcentrationSnapshot(
                context.SourceCategory!,
                context.SourceSubcategory!,
                context.Query!,
                context.SourceRegionDest!,
                context.Sort!,
                context.TopN,
                calculatedAtUtc);

        if (_dbContext.Entry(snapshot).State == EntityState.Detached)
            _dbContext.PublicMarketConcentrationSnapshots.Add(snapshot);

        snapshot.MarkCompleted(
            JsonSerializer.Serialize(live.MarketConcentration, JsonOptions),
            JsonSerializer.Serialize(live.PriceQualityMap.Points, JsonOptions),
            live.MarketConcentration.SampleSize,
            live.ObservationWindow.LatestObservedAtUtc,
            calculatedAtUtc,
            (long)stopwatch.Elapsed.TotalMilliseconds);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PublicMarketConcentrationSnapshotDto>.Success(Map(snapshot, includePoints: true));
    }

    private async Task MarkFailedAsync(PublicMarketIntelligenceQuery context, string error, CancellationToken cancellationToken)
    {
        var snapshot = await LoadSnapshotAsync(context, cancellationToken);
        if (snapshot is null)
        {
            var nowUtc = DateTime.UtcNow;
            snapshot = new PublicMarketConcentrationSnapshot(
                context.SourceCategory!,
                context.SourceSubcategory!,
                context.Query!,
                context.SourceRegionDest!,
                context.Sort!,
                context.TopN,
                nowUtc);
            _dbContext.PublicMarketConcentrationSnapshots.Add(snapshot);
        }

        snapshot.MarkFailed(DateTime.UtcNow, Truncate(error));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<PublicMarketConcentrationSnapshot?> LoadSnapshotAsync(
        PublicMarketIntelligenceQuery context,
        CancellationToken cancellationToken) =>
        _dbContext.PublicMarketConcentrationSnapshots
            .FirstOrDefaultAsync(x =>
                x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.Query == context.Query
                && x.SourceRegionDest == context.SourceRegionDest
                && x.Sort == context.Sort
                && x.TopN == context.TopN,
                cancellationToken);

    internal static PublicMarketConcentrationSnapshotDto Map(
        PublicMarketConcentrationSnapshot snapshot,
        bool includePoints)
    {
        var concentration = JsonSerializer.Deserialize<MarketConcentrationDto>(snapshot.MarketConcentrationJson, JsonOptions)
            ?? EmptyConcentration(snapshot.SampleSize);
        var points = includePoints
            ? JsonSerializer.Deserialize<List<PriceQualityPointDto>>(snapshot.PriceQualityPointsJson, JsonOptions) ?? []
            : [];
        var limitations = new List<string>();
        if (snapshot.Status == PublicMarketConcentrationSnapshot.FailedStatus && snapshot.CalculatedAtUtc.HasValue)
            limitations.Add("Показан последний успешный расчет. Последнее обновление концентрации завершилось ошибкой.");

        return new PublicMarketConcentrationSnapshotDto(
            new PublicMarketContextDto(
                "wildberries",
                snapshot.SourceCategory,
                snapshot.SourceSubcategory,
                snapshot.Query,
                snapshot.SourceRegionDest,
                snapshot.Sort,
                snapshot.TopN),
            new ObservationWindowDto(
                LatestRankRunId: null,
                BaselineRankRunId: null,
                LatestProductRunId: null,
                BaselineProductRunId: null,
                LatestObservedAtUtc: snapshot.LatestObservedAtUtc,
                BaselineObservedAtUtc: null,
                IsComparable: false,
                CoverageStatus: snapshot.Status,
                Limitations: limitations),
            concentration,
            new PriceQualityMapDto(points, BuildSummary(points), []),
            snapshot.SampleSize,
            snapshot.CalculatedAtUtc,
            snapshot.LatestObservedAtUtc,
            snapshot.Status,
            limitations);
    }

    private static MarketConcentrationDto EmptyConcentration(int sampleSize) =>
        new(
            sampleSize,
            0,
            0,
            0,
            0,
            0,
            0,
            [],
            [],
            [],
            [],
            string.Empty,
            []);

    private static PriceQualityMapSummaryDto BuildSummary(IReadOnlyList<PriceQualityPointDto> points)
    {
        var ratings = points
            .Select(x => x.Rating)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Order()
            .ToList();
        var prices = points
            .Select(x => x.Price)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Order()
            .ToList();

        return new PriceQualityMapSummaryDto(
            points.Count,
            points.Count(x => !x.Rating.HasValue || x.Rating <= 0),
            points.Count(x => x.QualityBucket == "strong"),
            points.Count(x => x.QualityBucket == "medium"),
            points.Count(x => x.QualityBucket == "weak"),
            points.Count(x => x.QualityBucket == "unknown"),
            Median(prices),
            Median(ratings),
            string.Empty);
    }

    private static decimal? Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
            return null;

        var middle = values.Count / 2;
        return values.Count % 2 == 1
            ? values[middle]
            : Math.Round((values[middle - 1] + values[middle]) / 2m, 2, MidpointRounding.AwayFromZero);
    }

    private static string Truncate(string value) =>
        value.Length <= 2000 ? value : value[..2000];
}
