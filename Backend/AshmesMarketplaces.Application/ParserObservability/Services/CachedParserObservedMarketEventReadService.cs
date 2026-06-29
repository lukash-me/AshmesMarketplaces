using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class CachedParserObservedMarketEventReadService : IParserObservedMarketEventReadService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;

    public CachedParserObservedMarketEventReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<ParserObservedMarketEventResponse>> GetListAsync(
        ParserObservedMarketEventQuery query,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadSnapshotAsync(cancellationToken);
        if (snapshot is null)
            return ServiceResult<ParserObservedMarketEventResponse>.NotFound("События остатков еще не рассчитаны.");

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = JsonSerializer.Deserialize<List<ParserObservedMarketEventItemDto>>(snapshot.MarketEventsJson, JsonOptions) ?? [];
        var summary = JsonSerializer.Deserialize<ParserObservedMarketEventSummaryDto>(snapshot.MarketEventSummaryJson, JsonOptions)
            ?? new ParserObservedMarketEventSummaryDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var filtered = items
            .Where(x => MatchesEventType(x, query.EventType))
            .Where(x => MatchesSearch(x, query.Search))
            .Where(x => MatchesText(x.SourceCategory, query.SourceCategory))
            .Where(x => MatchesText(x.SourceSubcategory, query.SourceSubcategory))
            .Where(x => MatchesText(x.BrandName, query.BrandName))
            .Where(x => MatchesText(x.SellerName, query.SellerName))
            .Where(x => MatchesMinQuantityChange(x, query.MinQuantityChange));

        var ordered = ApplySort(filtered, query.Sort).ToList();
        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return ServiceResult<ParserObservedMarketEventResponse>.Success(
            new ParserObservedMarketEventResponse(
                snapshot.CurrentLogisticsRunId,
                snapshot.PreviousLogisticsRunId,
                snapshot.CurrentObservedAtUtc,
                snapshot.PreviousObservedAtUtc,
                ordered.Count,
                page,
                pageSize,
                pageItems,
                summary,
                BuildWarnings(snapshot)));
    }

    private Task<PublicParserObservedLogisticsSnapshot?> LoadSnapshotAsync(CancellationToken cancellationToken) =>
        _dbContext.PublicParserObservedLogisticsSnapshots
            .AsNoTracking()
            .Where(x =>
                x.SnapshotKey == PublicParserObservedLogisticsSnapshot.SnapshotKeyValue
                && x.Status == PublicParserObservedLogisticsSnapshot.CompletedStatus
                && x.CalculatedAtUtc != null)
            .OrderByDescending(x => x.CalculatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private static bool MatchesEventType(ParserObservedMarketEventItemDto item, string? eventType) =>
        string.IsNullOrWhiteSpace(eventType) || string.Equals(item.EventType, eventType.Trim(), StringComparison.Ordinal);

    private static bool MatchesSearch(ParserObservedMarketEventItemDto item, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;
        var value = search.Trim();
        return Contains(item.Name, value)
            || Contains(item.WbProductId, value)
            || Contains(item.BrandName, value)
            || Contains(item.SellerName, value);
    }

    private static bool MatchesText(string? actual, string? expected) =>
        string.IsNullOrWhiteSpace(expected) || string.Equals(actual?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? actual, string search) =>
        actual?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    private static bool MatchesMinQuantityChange(ParserObservedMarketEventItemDto item, int? minQuantityChange)
    {
        if (item.EventType is not ParserObservedMarketEventTypes.StockDecreased
            and not ParserObservedMarketEventTypes.StockIncreased)
            return true;

        return Math.Abs(item.QuantityChange ?? 0) >= Math.Max(minQuantityChange ?? 1, 1);
    }

    private static IEnumerable<ParserObservedMarketEventItemDto> ApplySort(
        IEnumerable<ParserObservedMarketEventItemDto> items,
        string? sort)
    {
        return sort?.Trim() switch
        {
            "quantityChange" => items.OrderBy(x => x.QuantityChange ?? 0),
            "-quantityChange" => items.OrderByDescending(x => x.QuantityChange ?? 0),
            "price" => items.OrderBy(x => x.Price ?? decimal.MaxValue),
            "-price" => items.OrderByDescending(x => x.Price ?? decimal.MinValue),
            "rating" => items.OrderBy(x => x.Rating ?? decimal.MaxValue),
            "-rating" => items.OrderByDescending(x => x.Rating ?? decimal.MinValue),
            "feedbackCount" => items.OrderBy(x => x.FeedbackCount ?? int.MaxValue),
            "-feedbackCount" => items.OrderByDescending(x => x.FeedbackCount ?? int.MinValue),
            _ => items.OrderByDescending(x => x.CurrentObservedAtUtc ?? DateTime.MinValue)
        };
    }

    private static IReadOnlyList<string> BuildWarnings(PublicParserObservedLogisticsSnapshot snapshot)
    {
        var warnings = new List<string>();
        if (snapshot.CalculatedAtUtc.HasValue && DateTime.UtcNow - snapshot.CalculatedAtUtc.Value > TimeSpan.FromHours(36))
            warnings.Add("Данные ожидают обновления.");
        return warnings;
    }
}
