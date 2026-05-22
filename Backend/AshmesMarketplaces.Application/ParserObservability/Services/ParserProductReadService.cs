using System.Text.Json;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class ParserProductReadService : IParserProductReadService
{
    private readonly ApplicationDbContext _dbContext;

    public ParserProductReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ParserProductListItemDto>>> GetListAsync(
        ParserProductListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = ApplyFilters(_dbContext.ParserProductRows.AsNoTracking(), query);
        rows = ApplySort(rows, query.Sort);

        var totalCount = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pageRows.Select(MapToListItem).ToList();

        return ServiceResult<PagedResponse<ParserProductListItemDto>>.Success(
            new PagedResponse<ParserProductListItemDto>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<ParserProductDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ParserProductDetailDto>.BadRequest("Parser product row id is required.");

        var row = await _dbContext.ParserProductRows
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (row is null)
            return ServiceResult<ParserProductDetailDto>.NotFound("Parser product row was not found.");

        var sourceFile = await _dbContext.ParserFiles
            .AsNoTracking()
            .FirstAsync(x => x.Id == row.IdParserFile, cancellationToken);

        return ServiceResult<ParserProductDetailDto>.Success(MapToDetail(row, sourceFile));
    }

    private static IQueryable<ParserProductRow> ApplyFilters(
        IQueryable<ParserProductRow> rows,
        ParserProductListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.ParserRunId))
            rows = rows.Where(x => x.ParserRunId == query.ParserRunId.Trim());

        if (!string.IsNullOrWhiteSpace(query.SourceCategory))
            rows = rows.Where(x => x.SourceCategory == query.SourceCategory.Trim());

        if (!string.IsNullOrWhiteSpace(query.SourceSubcategory))
            rows = rows.Where(x => x.SourceSubcategory == query.SourceSubcategory.Trim());

        if (!string.IsNullOrWhiteSpace(query.BrandName))
            rows = rows.Where(x => x.BrandName == query.BrandName.Trim());

        if (!string.IsNullOrWhiteSpace(query.SellerName))
            rows = rows.Where(x => x.SellerName == query.SellerName.Trim());

        if (!string.IsNullOrWhiteSpace(query.WbRootId))
            rows = rows.Where(x => x.WbRootId == query.WbRootId.Trim());

        if (query.PriceDiscountedFrom.HasValue)
            rows = rows.Where(x => x.PriceDiscounted >= query.PriceDiscountedFrom.Value);

        if (query.PriceDiscountedTo.HasValue)
            rows = rows.Where(x => x.PriceDiscounted <= query.PriceDiscountedTo.Value);

        if (query.ReviewRatingFrom.HasValue)
            rows = rows.Where(x => x.ReviewRating >= query.ReviewRatingFrom.Value);

        if (query.ReviewRatingTo.HasValue)
            rows = rows.Where(x => x.ReviewRating <= query.ReviewRatingTo.Value);

        if (query.FeedbackCountFrom.HasValue)
            rows = rows.Where(x => x.FeedbackCount >= query.FeedbackCountFrom.Value);

        if (query.FeedbackCountTo.HasValue)
            rows = rows.Where(x => x.FeedbackCount <= query.FeedbackCountTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || EF.Functions.ILike(x.WbProductId, $"%{search}%")
                || (x.BrandName != null && EF.Functions.ILike(x.BrandName, $"%{search}%"))
                || (x.SellerName != null && EF.Functions.ILike(x.SellerName, $"%{search}%")));
        }

        return rows;
    }

    private static IOrderedQueryable<ParserProductRow> ApplySort(
        IQueryable<ParserProductRow> rows,
        string? sort)
    {
        return sort?.Trim() switch
        {
            "name" => rows.OrderBy(x => x.Name).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-name" => rows.OrderByDescending(x => x.Name).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            "parsedAtUtc" => rows.OrderBy(x => x.ParsedAtUtc).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "wbProductId" => rows.OrderBy(x => x.WbProductId).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-wbProductId" => rows.OrderByDescending(x => x.WbProductId).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            "priceDiscounted" => rows.OrderBy(x => x.PriceDiscounted).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-priceDiscounted" => rows.OrderByDescending(x => x.PriceDiscounted).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            "reviewRating" => rows.OrderBy(x => x.ReviewRating).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-reviewRating" => rows.OrderByDescending(x => x.ReviewRating).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            "feedbackCount" => rows.OrderBy(x => x.FeedbackCount).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-feedbackCount" => rows.OrderByDescending(x => x.FeedbackCount).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            _ => rows.OrderByDescending(x => x.ParsedAtUtc).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id)
        };
    }

    private static ParserProductListItemDto MapToListItem(ParserProductRow row)
    {
        return new ParserProductListItemDto(
            row.Id,
            row.ParserRunId,
            row.ParsedAtUtc,
            row.WbProductId,
            row.WbRootId,
            row.Name,
            row.BrandName,
            row.SellerName,
            row.PriceRegular,
            row.PriceDiscounted,
            row.PriceWbWallet,
            row.DiscountPercent,
            row.RatingRounded,
            row.ReviewRating,
            row.FeedbackCount,
            row.SourceCategory,
            row.SourceSubcategory,
            row.SourceQuery,
            GetImageUrls(row.ImageUrls).FirstOrDefault());
    }

    private static ParserProductDetailDto MapToDetail(ParserProductRow row, ParserFile sourceFile)
    {
        return new ParserProductDetailDto(
            row.Id,
            row.ParserRunId,
            row.ParsedAtUtc,
            row.WbProductId,
            row.WbRootId,
            row.Name,
            row.BrandName,
            row.SellerName,
            row.PriceRegular,
            row.PriceDiscounted,
            row.PriceWbWallet,
            row.DiscountPercent,
            row.RatingRounded,
            row.ReviewRating,
            row.FeedbackCount,
            row.SourceCategory,
            row.SourceSubcategory,
            row.SourceQuery,
            row.Marketplace,
            row.SkuProduct,
            row.Entity,
            row.BrandIdOnMp,
            row.SellerIdOnMp,
            row.TotalQuantity,
            row.FeedbackCountSource,
            GetImageUrls(row.ImageUrls),
            row.ImageCount,
            row.SubjectParentId,
            row.SubjectId,
            row.SourceRegionDest,
            sourceFile.Kind,
            sourceFile.Sha256,
            row.SourceLineNumber,
            row.RowHash);
    }

    private static IReadOnlyList<string> GetImageUrls(JsonDocument? imageUrls)
    {
        if (imageUrls is null || imageUrls.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return imageUrls.RootElement
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();
    }
}
