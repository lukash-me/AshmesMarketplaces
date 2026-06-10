using System.Globalization;
using System.Text.Json;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.WorkspaceMarketProducts.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.WorkspaceMarketProducts.Services;

public sealed class WorkspaceMarketProductService : IWorkspaceMarketProductService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public WorkspaceMarketProductService(ApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<PagedResponse<WorkspaceMarketProductListItemResponse>>> GetListAsync(
        Guid workspaceId,
        WorkspaceMarketProductListQuery query,
        CancellationToken cancellationToken)
    {
        var access = await EnsureWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!access.IsSuccess)
            return PropagateError<PagedResponse<WorkspaceMarketProductListItemResponse>>(access.Error!);

        var tagCheck = ValidateOptionalTag(query.TagKey);
        if (tagCheck is not null)
            return ServiceResult<PagedResponse<WorkspaceMarketProductListItemResponse>>.BadRequest(tagCheck);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = ApplyFilters(_dbContext.WorkspaceMarketProducts.AsNoTracking().Where(x => x.IdWorkspace == workspaceId), query);
        rows = ApplySort(rows, query.Sort);

        var totalCount = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var latestObserved = await LoadLatestObservedMapAsync(pageRows, cancellationToken);
        var items = pageRows.Select(row => MapToListItem(row, latestObserved.GetValueOrDefault(row.Id))).ToList();

        return ServiceResult<PagedResponse<WorkspaceMarketProductListItemResponse>>.Success(
            new PagedResponse<WorkspaceMarketProductListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<WorkspaceMarketProductResponse>> GetByIdAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rowResult = await LoadOwnedRowAsync(workspaceId, id, asNoTracking: true, cancellationToken);
        if (!rowResult.IsSuccess)
            return PropagateError<WorkspaceMarketProductResponse>(rowResult.Error!);

        var row = rowResult.Value;
        if (row is null)
            return ServiceResult<WorkspaceMarketProductResponse>.NotFound("Workspace market product was not found.");

        var latestObserved = await LoadLatestObservedAtAsync(row, cancellationToken);
        return ServiceResult<WorkspaceMarketProductResponse>.Success(MapToResponse(row, latestObserved));
    }

    public async Task<ServiceResult<WorkspaceMarketProductResponse>> AddAsync(
        Guid workspaceId,
        CreateWorkspaceMarketProductRequest request,
        CancellationToken cancellationToken)
    {
        var access = await EnsureWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!access.IsSuccess)
            return PropagateError<WorkspaceMarketProductResponse>(access.Error!);

        var userId = _currentUser.UserId!.Value;
        var tagCheck = ValidateRequiredTag(request.TagKey);
        if (tagCheck is not null)
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest(tagCheck);

        if (request.ParserProductRowId == Guid.Empty)
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest("Parser product row id is required.");

        var productRow = await _dbContext.ParserProductRows
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ParserProductRowId, cancellationToken);
        if (productRow is null)
            return ServiceResult<WorkspaceMarketProductResponse>.NotFound("Market product was not found.");

        var existing = await _dbContext.WorkspaceMarketProducts
            .FirstOrDefaultAsync(x =>
                x.IdWorkspace == workspaceId
                && x.WbProductId == productRow.WbProductId
                && x.SourceSubcategoryKey == BuildContextKey(productRow.SourceSubcategory)
                && x.SourceRegionDestKey == BuildContextKey(productRow.SourceRegionDest),
                cancellationToken);

        var now = DateTime.UtcNow;
        var snapshot = await BuildSnapshotAsync(productRow, cancellationToken);
        if (existing is not null)
        {
            existing.UpdateTracking(request.TagKey, request.Note, now);
            existing.RefreshSnapshot(
                productRow.Id,
                productRow.Name,
                productRow.BrandName,
                productRow.SellerName,
                ExtractFirstImage(productRow.ImageUrls),
                productRow.PriceRegular,
                productRow.PriceDiscounted,
                productRow.PriceWbWallet,
                productRow.ReviewRating,
                productRow.FeedbackCount,
                snapshot.PositionAbsolute,
                snapshot.TotalQuantity,
                now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<WorkspaceMarketProductResponse>.Success(
                MapToResponse(existing, await LoadLatestObservedAtAsync(existing, cancellationToken)));
        }

        try
        {
            var row = new WorkspaceMarketProduct(
                workspaceId,
                userId,
                productRow.Id,
                productRow.WbProductId,
                productRow.WbRootId,
                productRow.SourceCategory,
                productRow.SourceSubcategory,
                productRow.SourceRegionDest,
                productRow.SourceQuery,
                request.TagKey,
                request.Note,
                productRow.Name,
                productRow.BrandName,
                productRow.SellerName,
                ExtractFirstImage(productRow.ImageUrls),
                productRow.PriceRegular,
                productRow.PriceDiscounted,
                productRow.PriceWbWallet,
                productRow.ReviewRating,
                productRow.FeedbackCount,
                snapshot.PositionAbsolute,
                snapshot.TotalQuantity,
                now,
                now);

            _dbContext.WorkspaceMarketProducts.Add(row);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<WorkspaceMarketProductResponse>.Success(
                MapToResponse(row, await LoadLatestObservedAtAsync(row, cancellationToken)));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<WorkspaceMarketProductResponse>.Conflict("Market product cannot be added because it conflicts with existing workspace data.");
        }
    }

    public async Task<ServiceResult<WorkspaceMarketProductResponse>> UpdateAsync(
        Guid workspaceId,
        Guid id,
        UpdateWorkspaceMarketProductRequest request,
        CancellationToken cancellationToken)
    {
        var rowResult = await LoadOwnedRowAsync(workspaceId, id, asNoTracking: false, cancellationToken);
        if (!rowResult.IsSuccess)
            return PropagateError<WorkspaceMarketProductResponse>(rowResult.Error!);

        var row = rowResult.Value;
        if (row is null)
            return ServiceResult<WorkspaceMarketProductResponse>.NotFound("Workspace market product was not found.");

        var tagCheck = ValidateRequiredTag(request.TagKey);
        if (tagCheck is not null)
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest(tagCheck);

        row.UpdateTracking(request.TagKey, request.Note, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<WorkspaceMarketProductResponse>.Success(
            MapToResponse(row, await LoadLatestObservedAtAsync(row, cancellationToken)));
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rowResult = await LoadOwnedRowAsync(workspaceId, id, asNoTracking: false, cancellationToken);
        if (!rowResult.IsSuccess)
            return PropagateError(rowResult.Error!);

        var row = rowResult.Value;
        if (row is null)
            return ServiceResult.NotFound("Workspace market product was not found.");

        _dbContext.WorkspaceMarketProducts.Remove(row);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<WorkspaceMarketProductHistoryResponse>> GetHistoryAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rowResult = await LoadOwnedRowAsync(workspaceId, id, asNoTracking: true, cancellationToken);
        if (!rowResult.IsSuccess)
            return PropagateError<WorkspaceMarketProductHistoryResponse>(rowResult.Error!);

        var row = rowResult.Value;
        if (row is null)
            return ServiceResult<WorkspaceMarketProductHistoryResponse>.NotFound("Workspace market product was not found.");

        var groups = new List<WorkspaceMarketProductHistoryGroupResponse>
        {
            await BuildProductHistoryGroupAsync(row, "price", "Цена", x => x.PriceWbWallet ?? x.PriceDiscounted ?? x.PriceRegular, "₽", cancellationToken),
            await BuildProductHistoryGroupAsync(row, "rating", "Рейтинг", x => x.ReviewRating, null, cancellationToken),
            await BuildProductHistoryGroupAsync(row, "feedbacks", "Отзывы", x => x.FeedbackCount, null, cancellationToken),
            await BuildPositionHistoryGroupAsync(row, cancellationToken),
            await BuildStockHistoryGroupAsync(row, cancellationToken)
        };

        return ServiceResult<WorkspaceMarketProductHistoryResponse>.Success(
            new WorkspaceMarketProductHistoryResponse(row.Id, row.WbProductId, groups));
    }

    private async Task<ServiceResult> EnsureWorkspaceAccessAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty)
            return ServiceResult.BadRequest("Workspace id is required.");

        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            return ServiceResult.Unauthorized("Authentication is required.");

        var exists = await _dbContext.Workspaces.AsNoTracking().AnyAsync(x => x.Id == workspaceId, cancellationToken);
        if (!exists)
            return ServiceResult.NotFound("Workspace was not found.");

        var hasAccess = await _dbContext.UserWorkspaces
            .AsNoTracking()
            .AnyAsync(x => x.IdWorkspace == workspaceId && x.IdUser == _currentUser.UserId.Value, cancellationToken);

        return hasAccess
            ? ServiceResult.Success()
            : ServiceResult.Forbidden("User does not have access to this workspace.");
    }

    private async Task<ServiceResult<WorkspaceMarketProduct?>> LoadOwnedRowAsync(
        Guid workspaceId,
        Guid id,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var access = await EnsureWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!access.IsSuccess)
            return access.Error!.Type switch
            {
                ServiceErrorType.BadRequest => ServiceResult<WorkspaceMarketProduct?>.BadRequest(access.Error.Message),
                ServiceErrorType.NotFound => ServiceResult<WorkspaceMarketProduct?>.NotFound(access.Error.Message),
                ServiceErrorType.Unauthorized => ServiceResult<WorkspaceMarketProduct?>.Unauthorized(access.Error.Message),
                _ => ServiceResult<WorkspaceMarketProduct?>.Forbidden(access.Error.Message)
            };

        if (id == Guid.Empty)
            return ServiceResult<WorkspaceMarketProduct?>.BadRequest("Workspace market product id is required.");

        var rows = _dbContext.WorkspaceMarketProducts.Where(x => x.IdWorkspace == workspaceId && x.Id == id);
        if (asNoTracking)
            rows = rows.AsNoTracking();

        return ServiceResult<WorkspaceMarketProduct?>.Success(await rows.FirstOrDefaultAsync(cancellationToken));
    }

    private static IQueryable<WorkspaceMarketProduct> ApplyFilters(
        IQueryable<WorkspaceMarketProduct> rows,
        WorkspaceMarketProductListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.TagKey))
        {
            var tag = query.TagKey.Trim();
            rows = rows.Where(x => x.TagKey == tag);
        }

        if (query.ParserProductRowId.HasValue)
            rows = rows.Where(x => x.ParserProductRowId == query.ParserProductRowId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || EF.Functions.ILike(x.WbProductId, $"%{search}%")
                || (x.BrandName != null && EF.Functions.ILike(x.BrandName, $"%{search}%"))
                || (x.SellerName != null && EF.Functions.ILike(x.SellerName, $"%{search}%"))
                || (x.Note != null && EF.Functions.ILike(x.Note, $"%{search}%")));
        }

        return rows;
    }

    private static IQueryable<WorkspaceMarketProduct> ApplySort(IQueryable<WorkspaceMarketProduct> rows, string? sort)
    {
        return sort?.Trim() switch
        {
            "name" => rows.OrderBy(x => x.Name),
            "-name" => rows.OrderByDescending(x => x.Name),
            "dateUpdate" => rows.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => rows.OrderByDescending(x => x.DateUpdate),
            "price" => rows.OrderBy(x => x.PriceWbWallet ?? x.PriceDiscounted ?? x.PriceRegular),
            "-price" => rows.OrderByDescending(x => x.PriceWbWallet ?? x.PriceDiscounted ?? x.PriceRegular),
            "position" => rows.OrderBy(x => x.PositionAbsolute ?? int.MaxValue),
            "-position" => rows.OrderByDescending(x => x.PositionAbsolute ?? 0),
            _ => rows.OrderByDescending(x => x.DateUpdate)
        };
    }

    private async Task<MarketProductSnapshot> BuildSnapshotAsync(ParserProductRow row, CancellationToken cancellationToken)
    {
        var position = await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.WbProductId == row.WbProductId
                && x.SourceSubcategory == row.SourceSubcategory
                && x.SourceRegionDest == row.SourceRegionDest)
            .OrderByDescending(x => x.ObservedAtUtc)
            .ThenBy(x => x.AbsolutePosition)
            .Select(x => (int?)x.AbsolutePosition)
            .FirstOrDefaultAsync(cancellationToken);

        var quantity = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.WbProductId == row.WbProductId
                && x.SourceSubcategory == row.SourceSubcategory
                && x.SourceRegionDest == row.SourceRegionDest)
            .OrderByDescending(x => x.ObservedAtUtc)
            .Select(x => x.TotalQuantityObserved)
            .FirstOrDefaultAsync(cancellationToken);

        return new MarketProductSnapshot(position, quantity);
    }

    private async Task<Dictionary<Guid, DateTime?>> LoadLatestObservedMapAsync(
        IReadOnlyList<WorkspaceMarketProduct> rows,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, DateTime?>();
        foreach (var row in rows)
        {
            result[row.Id] = await LoadLatestObservedAtAsync(row, cancellationToken);
        }

        return result;
    }

    private async Task<DateTime?> LoadLatestObservedAtAsync(WorkspaceMarketProduct row, CancellationToken cancellationToken)
    {
        var productDate = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x =>
                x.WbProductId == row.WbProductId
                && x.SourceSubcategory == row.SourceSubcategory
                && x.SourceRegionDest == row.SourceRegionDest)
            .MaxAsync(x => (DateTime?)x.ParsedAtUtc, cancellationToken);
        var rankDate = await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.WbProductId == row.WbProductId
                && x.SourceSubcategory == row.SourceSubcategory
                && x.SourceRegionDest == row.SourceRegionDest)
            .MaxAsync(x => (DateTime?)x.ObservedAtUtc, cancellationToken);
        var logisticsDate = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.WbProductId == row.WbProductId
                && x.SourceSubcategory == row.SourceSubcategory
                && x.SourceRegionDest == row.SourceRegionDest)
            .MaxAsync(x => (DateTime?)x.ObservedAtUtc, cancellationToken);

        return new[] { productDate, rankDate, logisticsDate }
            .Where(x => x.HasValue)
            .DefaultIfEmpty()
            .Max();
    }

    private async Task<WorkspaceMarketProductHistoryGroupResponse> BuildProductHistoryGroupAsync(
        WorkspaceMarketProduct row,
        string key,
        string label,
        Func<ParserProductRow, decimal?> selector,
        string? suffix,
        CancellationToken cancellationToken)
    {
        var items = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x =>
                x.WbProductId == row.WbProductId
                && x.SourceSubcategory == row.SourceSubcategory
                && x.SourceRegionDest == row.SourceRegionDest)
            .OrderBy(x => x.ParsedAtUtc)
            .ToListAsync(cancellationToken);

        var points = items
            .Select(x => new WorkspaceMarketProductHistoryPointResponse(
                x.ParsedAtUtc,
                selector(x),
                FormatValue(selector(x), suffix),
                x.SourceSubcategory))
            .Where(x => x.Value.HasValue)
            .ToList();

        return new WorkspaceMarketProductHistoryGroupResponse(key, label, points);
    }

    private async Task<WorkspaceMarketProductHistoryGroupResponse> BuildPositionHistoryGroupAsync(
        WorkspaceMarketProduct row,
        CancellationToken cancellationToken)
    {
        var points = await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.WbProductId == row.WbProductId
                && x.SourceSubcategory == row.SourceSubcategory
                && x.SourceRegionDest == row.SourceRegionDest)
            .OrderBy(x => x.ObservedAtUtc)
            .Select(x => new WorkspaceMarketProductHistoryPointResponse(
                x.ObservedAtUtc,
                (decimal?)x.AbsolutePosition,
                $"#{x.AbsolutePosition}",
                x.Query))
            .ToListAsync(cancellationToken);

        return new WorkspaceMarketProductHistoryGroupResponse("position", "Позиция", points);
    }

    private async Task<WorkspaceMarketProductHistoryGroupResponse> BuildStockHistoryGroupAsync(
        WorkspaceMarketProduct row,
        CancellationToken cancellationToken)
    {
        var points = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.WbProductId == row.WbProductId
                && x.SourceSubcategory == row.SourceSubcategory
                && x.SourceRegionDest == row.SourceRegionDest)
            .OrderBy(x => x.ObservedAtUtc)
            .Select(x => new WorkspaceMarketProductHistoryPointResponse(
                x.ObservedAtUtc,
                (decimal?)x.TotalQuantityObserved,
                x.TotalQuantityObserved.HasValue ? x.TotalQuantityObserved.Value.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ") : "Нет данных",
                x.SourceRegionDest))
            .ToListAsync(cancellationToken);

        return new WorkspaceMarketProductHistoryGroupResponse("stock", "Остатки", points);
    }

    private static WorkspaceMarketProductListItemResponse MapToListItem(WorkspaceMarketProduct row, DateTime? latestObservedAtUtc) =>
        new(
            row.Id,
            row.IdWorkspace,
            row.ParserProductRowId,
            row.WbProductId,
            row.WbRootId,
            row.SourceCategory,
            row.SourceSubcategory,
            row.SourceRegionDest,
            row.SourceQuery,
            row.TagKey,
            row.Note,
            row.Name,
            row.BrandName,
            row.SellerName,
            row.ThumbnailUrl,
            row.PriceRegular,
            row.PriceDiscounted,
            row.PriceWbWallet,
            row.ReviewRating,
            row.FeedbackCount,
            row.PositionAbsolute,
            row.TotalQuantity,
            row.DateCreate,
            row.DateUpdate,
            latestObservedAtUtc);

    private static WorkspaceMarketProductResponse MapToResponse(WorkspaceMarketProduct row, DateTime? latestObservedAtUtc) =>
        new(
            row.Id,
            row.IdWorkspace,
            row.IdCreatedByUser,
            row.ParserProductRowId,
            row.WbProductId,
            row.WbRootId,
            row.SourceCategory,
            row.SourceSubcategory,
            row.SourceRegionDest,
            row.SourceQuery,
            row.TagKey,
            row.Note,
            row.Name,
            row.BrandName,
            row.SellerName,
            row.ThumbnailUrl,
            row.PriceRegular,
            row.PriceDiscounted,
            row.PriceWbWallet,
            row.ReviewRating,
            row.FeedbackCount,
            row.PositionAbsolute,
            row.TotalQuantity,
            row.DateCreate,
            row.DateUpdate,
            latestObservedAtUtc);

    private static string? ValidateRequiredTag(string? tagKey)
    {
        if (string.IsNullOrWhiteSpace(tagKey))
            return "Tag key is required.";

        return WorkspaceMarketProduct.IsValidTag(tagKey.Trim())
            ? null
            : "Tag key must be one of: competitor, idea.";
    }

    private static string? ValidateOptionalTag(string? tagKey)
    {
        if (string.IsNullOrWhiteSpace(tagKey))
            return null;

        return WorkspaceMarketProduct.IsValidTag(tagKey.Trim())
            ? null
            : "Tag key must be one of: competitor, idea.";
    }

    private static string BuildContextKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static ServiceResult PropagateError(ServiceError error) =>
        error.Type switch
        {
            ServiceErrorType.BadRequest => ServiceResult.BadRequest(error.Message),
            ServiceErrorType.NotFound => ServiceResult.NotFound(error.Message),
            ServiceErrorType.Conflict => ServiceResult.Conflict(error.Message),
            ServiceErrorType.Forbidden => ServiceResult.Forbidden(error.Message),
            ServiceErrorType.Unauthorized => ServiceResult.Unauthorized(error.Message),
            ServiceErrorType.Unavailable => ServiceResult.Unavailable(error.Message),
            _ => ServiceResult.Unavailable(error.Message)
        };

    private static ServiceResult<T> PropagateError<T>(ServiceError error) =>
        error.Type switch
        {
            ServiceErrorType.BadRequest => ServiceResult<T>.BadRequest(error.Message),
            ServiceErrorType.NotFound => ServiceResult<T>.NotFound(error.Message),
            ServiceErrorType.Conflict => ServiceResult<T>.Conflict(error.Message),
            ServiceErrorType.Forbidden => ServiceResult<T>.Forbidden(error.Message),
            ServiceErrorType.Unauthorized => ServiceResult<T>.Unauthorized(error.Message),
            ServiceErrorType.Unavailable => ServiceResult<T>.Unavailable(error.Message),
            _ => ServiceResult<T>.Unavailable(error.Message)
        };

    private static string FormatValue(decimal? value, string? suffix)
    {
        if (!value.HasValue)
            return "Нет данных";

        var formatted = value.Value % 1 == 0
            ? value.Value.ToString("N0", CultureInfo.InvariantCulture)
            : value.Value.ToString("N2", CultureInfo.InvariantCulture);

        formatted = formatted.Replace(",", " ");
        return string.IsNullOrWhiteSpace(suffix) ? formatted : $"{formatted} {suffix}";
    }

    private static string? ExtractFirstImage(JsonDocument? imageUrls)
    {
        if (imageUrls is null || imageUrls.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in imageUrls.RootElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return null;
    }

    private sealed record MarketProductSnapshot(int? PositionAbsolute, int? TotalQuantity);
}
