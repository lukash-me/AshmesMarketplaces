using System.Text.Json;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class PublicProductAvailabilityReadService : IPublicProductAvailabilityReadService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;

    public PublicProductAvailabilityReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ParserProductListItemDto>>> GetAsync(
        ParserProductListQuery query,
        CancellationToken cancellationToken)
    {
        var snapshot = await _dbContext.PublicProductAvailabilitySnapshots
            .AsNoTracking()
            .Where(x =>
                x.SnapshotKey == PublicProductAvailabilitySnapshot.SnapshotKeyValue
                && x.Status == PublicProductAvailabilitySnapshot.CompletedStatus
                && x.CalculatedAtUtc != null)
            .OrderByDescending(x => x.CalculatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
            return ServiceResult<PagedResponse<ParserProductListItemDto>>.NotFound("Доступность товаров еще не рассчитана.");

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = JsonSerializer.Deserialize<List<ParserProductListItemDto>>(snapshot.ItemsJson, JsonOptions) ?? [];
        var filtered = items
            .Where(x => MatchesSearch(x, query.Search))
            .Where(x => MatchesText(x.SourceCategory, query.SourceCategory))
            .Where(x => MatchesText(x.SourceSubcategory, query.SourceSubcategory))
            .Where(x => MatchesText(x.BrandName, query.BrandName))
            .Where(x => MatchesText(x.SellerName, query.SellerName));

        var ordered = ApplySort(filtered, query.Sort).ToList();
        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return ServiceResult<PagedResponse<ParserProductListItemDto>>.Success(
            new PagedResponse<ParserProductListItemDto>(pageItems, page, pageSize, ordered.Count));
    }

    private static bool MatchesSearch(ParserProductListItemDto item, string? search)
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

    private static IEnumerable<ParserProductListItemDto> ApplySort(IEnumerable<ParserProductListItemDto> items, string? sort)
    {
        return sort?.Trim() switch
        {
            "price" => items.OrderBy(x => CurrentPrice(x) ?? decimal.MaxValue),
            "-price" => items.OrderByDescending(x => CurrentPrice(x) ?? decimal.MinValue),
            "rating" => items.OrderBy(x => x.ReviewRating ?? decimal.MaxValue),
            "-rating" => items.OrderByDescending(x => x.ReviewRating ?? decimal.MinValue),
            "feedbackCount" => items.OrderBy(x => x.FeedbackCount ?? int.MaxValue),
            "-feedbackCount" => items.OrderByDescending(x => x.FeedbackCount ?? int.MinValue),
            "stock" => items.OrderBy(x => x.Logistics?.TotalQuantityObserved ?? int.MaxValue),
            "-stock" => items.OrderByDescending(x => x.Logistics?.TotalQuantityObserved ?? int.MinValue),
            _ => items.OrderByDescending(x => x.ParsedAtUtc)
        };
    }

    private static decimal? CurrentPrice(ParserProductListItemDto item) =>
        item.PriceDiscounted ?? item.PriceWbWallet ?? item.PriceRegular;
}
