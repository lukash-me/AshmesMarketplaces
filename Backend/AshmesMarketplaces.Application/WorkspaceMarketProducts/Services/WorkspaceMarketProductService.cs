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
using Microsoft.Extensions.Logging;

namespace AshmesMarketplaces.Application.WorkspaceMarketProducts.Services;

public sealed class WorkspaceMarketProductService : IWorkspaceMarketProductService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IWorkspaceMarketProductMediaStorage _mediaStorage;
    private readonly ILogger<WorkspaceMarketProductService> _logger;

    public WorkspaceMarketProductService(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IWorkspaceMarketProductMediaStorage mediaStorage,
        ILogger<WorkspaceMarketProductService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _mediaStorage = mediaStorage;
        _logger = logger;
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
        var media = await LoadMediaMapAsync(pageRows.Select(x => x.Id).ToArray(), cancellationToken);
        var items = pageRows.Select(row => MapToListItem(row, latestObserved.GetValueOrDefault(row.Id), media.GetValueOrDefault(row.Id) ?? [])).ToList();

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
        var media = await LoadMediaAsync(row.Id, cancellationToken);
        return ServiceResult<WorkspaceMarketProductResponse>.Success(MapToResponse(row, latestObserved, media));
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
            var media = await LoadMediaAsync(existing.Id, cancellationToken);
            return ServiceResult<WorkspaceMarketProductResponse>.Success(
                MapToResponse(existing, await LoadLatestObservedAtAsync(existing, cancellationToken), media));
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
                WorkspaceMarketProduct.ParserSourceType,
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
                costPrice: null,
                description: null,
                characteristicsJson: null,
                supplierName: null,
                supplierUrl: null,
                demoPayloadJson: null,
                now,
                now);

            _dbContext.WorkspaceMarketProducts.Add(row);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<WorkspaceMarketProductResponse>.Success(
                MapToResponse(row, await LoadLatestObservedAtAsync(row, cancellationToken), []));
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

    public async Task<ServiceResult<WorkspaceMarketProductResponse>> AddDemoAsync(
        Guid workspaceId,
        CreateDemoWorkspaceMarketProductRequest request,
        IReadOnlyList<WorkspaceMarketProductUpload> media,
        CancellationToken cancellationToken)
    {
        var access = await EnsureWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!access.IsSuccess)
            return PropagateError<WorkspaceMarketProductResponse>(access.Error!);

        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest("Name is required.");

        if (string.IsNullOrWhiteSpace(request.SourceCategory))
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest("Category is required.");

        if (string.IsNullOrWhiteSpace(request.SourceSubcategory))
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest("Subcategory is required.");

        if (request.Price <= 0)
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest("Price must be positive.");

        if (request.CostPrice.HasValue && request.CostPrice.Value < 0)
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest("Cost price cannot be negative.");

        if (media.Count > 10)
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest("At most 10 media files can be uploaded.");

        var tagKey = WorkspaceMarketProduct.CreatedTag;
        var tagCheck = ValidateRequiredTag(tagKey);
        if (tagCheck is not null)
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest(tagCheck);

        if (!IsJsonOrEmpty(request.CharacteristicsJson))
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest("Characteristics must be valid JSON.");

        var now = DateTime.UtcNow;
        var row = new WorkspaceMarketProduct(
            workspaceId,
            _currentUser.UserId!.Value,
            parserProductRowId: null,
            wbProductId: null,
            wbRootId: null,
            request.SourceCategory,
            request.SourceSubcategory,
            sourceRegionDest: null,
            sourceQuery: null,
            WorkspaceMarketProduct.DemoSourceType,
            tagKey,
            request.Note,
            request.Name,
            brandName: null,
            sellerName: request.SupplierName,
            thumbnailUrl: null,
            priceRegular: request.Price,
            priceDiscounted: request.Price,
            priceWbWallet: request.Price,
            reviewRating: null,
            feedbackCount: null,
            positionAbsolute: null,
            totalQuantity: null,
            request.CostPrice,
            request.Description,
            NormalizeJson(request.CharacteristicsJson),
            request.SupplierName,
            request.SupplierUrl,
            demoPayloadJson: null,
            now,
            now);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.WorkspaceMarketProducts.Add(row);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var savedMedia = new List<WorkspaceMarketProductMedia>();
            var sortOrder = 1;
            foreach (var upload in media)
            {
                var stored = await _mediaStorage.SaveAsync(workspaceId, row.Id, upload, sortOrder, cancellationToken);
                savedMedia.Add(new WorkspaceMarketProductMedia(
                    row.Id,
                    stored.Url,
                    stored.StorageKey,
                    stored.FileName,
                    stored.ContentType,
                    sortOrder,
                    WorkspaceMarketProductMedia.ImageKind,
                    DateTime.UtcNow));
                sortOrder++;
            }

            if (savedMedia.Count > 0)
            {
                _dbContext.WorkspaceMarketProductMedia.AddRange(savedMedia);
                row.SetThumbnail(savedMedia.OrderBy(x => x.SortOrder).First().Url, DateTime.UtcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<WorkspaceMarketProductResponse>.Success(
                MapToResponse(row, row.DateUpdate, savedMedia.Select(MapMedia).ToList()));
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<WorkspaceMarketProductResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<WorkspaceMarketProductResponse>.Conflict("Demo market product cannot be created because it conflicts with existing workspace data.");
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

        var media = await LoadMediaAsync(row.Id, cancellationToken);
        return ServiceResult<WorkspaceMarketProductResponse>.Success(
            MapToResponse(row, await LoadLatestObservedAtAsync(row, cancellationToken), media));
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

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _dbContext.WorkspaceMarketProductUserReadStates
                .Where(x => x.IdWorkspaceMarketProduct == row.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.WorkspaceMarketProductAnalyses
                .Where(x => x.IdWorkspaceMarketProduct == row.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.WorkspaceMarketProductMedia
                .Where(x => x.IdWorkspaceMarketProduct == row.Id)
                .ExecuteDeleteAsync(cancellationToken);

            _dbContext.WorkspaceMarketProducts.Remove(row);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(
                exception,
                "Failed to delete workspace market product {WorkspaceMarketProductId} from workspace {WorkspaceId}. SourceType={SourceType}, WbProductId={WbProductId}, ParserProductRowId={ParserProductRowId}.",
                row.Id,
                workspaceId,
                row.SourceType,
                row.WbProductId,
                row.ParserProductRowId);
            return ServiceResult.Conflict("Не удалось удалить карточку из наблюдаемых.");
        }

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

        if (row.SourceType == WorkspaceMarketProduct.DemoSourceType)
        {
            var demoGroups = new List<WorkspaceMarketProductHistoryGroupResponse>
            {
                BuildDemoHistoryGroup(row, "price", "Цена", row.PriceWbWallet ?? row.PriceDiscounted ?? row.PriceRegular, "₽"),
                BuildDemoHistoryGroup(row, "rating", "Рейтинг", row.ReviewRating, null),
                BuildDemoHistoryGroup(row, "feedbacks", "Отзывы", row.FeedbackCount, null),
                BuildDemoHistoryGroup(row, "position", "Позиция", row.PositionAbsolute, null),
                BuildDemoHistoryGroup(row, "stock", "Остатки", row.TotalQuantity, null)
            };

            return ServiceResult<WorkspaceMarketProductHistoryResponse>.Success(
                new WorkspaceMarketProductHistoryResponse(row.Id, row.WbProductId, demoGroups));
        }

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
                || (x.WbProductId != null && EF.Functions.ILike(x.WbProductId, $"%{search}%"))
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
        if (row.SourceType == WorkspaceMarketProduct.DemoSourceType || string.IsNullOrWhiteSpace(row.WbProductId))
            return row.DateUpdate;

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

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<WorkspaceMarketProductMediaResponse>>> LoadMediaMapAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<WorkspaceMarketProductMediaResponse>>();

        var rows = await _dbContext.WorkspaceMarketProductMedia
            .AsNoTracking()
            .Where(x => productIds.Contains(x.IdWorkspaceMarketProduct))
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.IdWorkspaceMarketProduct)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<WorkspaceMarketProductMediaResponse>)x.Select(MapMedia).ToList());
    }

    private async Task<IReadOnlyList<WorkspaceMarketProductMediaResponse>> LoadMediaAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var media = await LoadMediaMapAsync([productId], cancellationToken);
        return media.GetValueOrDefault(productId) ?? [];
    }

    private static WorkspaceMarketProductHistoryGroupResponse BuildDemoHistoryGroup(
        WorkspaceMarketProduct row,
        string key,
        string label,
        decimal? value,
        string? suffix)
    {
        var items = value.HasValue
            ? new List<WorkspaceMarketProductHistoryPointResponse>
            {
                new(row.DateUpdate, value, FormatValue(value, suffix), row.SourceSubcategory)
            }
            : [];

        return new WorkspaceMarketProductHistoryGroupResponse(key, label, items);
    }

    private static WorkspaceMarketProductMediaResponse MapMedia(WorkspaceMarketProductMedia row) =>
        new(row.Id, row.Url, row.FileName, row.ContentType, row.SortOrder, row.Kind, row.UploadedAtUtc);

    private static WorkspaceMarketProductListItemResponse MapToListItem(
        WorkspaceMarketProduct row,
        DateTime? latestObservedAtUtc,
        IReadOnlyList<WorkspaceMarketProductMediaResponse> media) =>
        new(
            row.Id,
            row.IdWorkspace,
            row.ParserProductRowId,
            row.WbProductId,
            row.WbRootId,
            row.SourceType,
            row.SourceType == WorkspaceMarketProduct.DemoSourceType,
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
            row.CostPrice,
            row.Description,
            row.CharacteristicsJson,
            row.SupplierName,
            row.SupplierUrl,
            media,
            row.DateCreate,
            row.DateUpdate,
            latestObservedAtUtc);

    private static WorkspaceMarketProductResponse MapToResponse(
        WorkspaceMarketProduct row,
        DateTime? latestObservedAtUtc,
        IReadOnlyList<WorkspaceMarketProductMediaResponse> media) =>
        new(
            row.Id,
            row.IdWorkspace,
            row.IdCreatedByUser,
            row.ParserProductRowId,
            row.WbProductId,
            row.WbRootId,
            row.SourceType,
            row.SourceType == WorkspaceMarketProduct.DemoSourceType,
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
            row.CostPrice,
            row.Description,
            row.CharacteristicsJson,
            row.SupplierName,
            row.SupplierUrl,
            media,
            row.DateCreate,
            row.DateUpdate,
            latestObservedAtUtc);

    private static string? ValidateRequiredTag(string? tagKey)
    {
        if (string.IsNullOrWhiteSpace(tagKey))
            return "Tag key is required.";

        return WorkspaceMarketProduct.IsValidTag(tagKey.Trim())
            ? null
            : "Tag key must be one of: competitor, idea, created.";
    }

    private static string? ValidateOptionalTag(string? tagKey)
    {
        if (string.IsNullOrWhiteSpace(tagKey))
            return null;

        return WorkspaceMarketProduct.IsValidTag(tagKey.Trim())
            ? null
            : "Tag key must be one of: competitor, idea, created.";
    }

    private static bool IsJsonOrEmpty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        using var document = JsonDocument.Parse(value);
        return JsonSerializer.Serialize(document.RootElement);
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
