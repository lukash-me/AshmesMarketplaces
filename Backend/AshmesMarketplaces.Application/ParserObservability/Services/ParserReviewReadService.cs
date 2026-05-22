using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class ParserReviewReadService : IParserReviewReadService
{
    private readonly ApplicationDbContext _dbContext;

    public ParserReviewReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ParserReviewListItemDto>>> GetListAsync(
        ParserReviewListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = ApplyFilters(_dbContext.ParserReviewRows.AsNoTracking(), query);
        rows = ApplySort(rows, query.Sort);

        var totalCount = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(review => new
            {
                Review = review,
                HasObservedReply = _dbContext.ParserReviewReplyRows.Any(reply =>
                    reply.IdParserRun == review.IdParserRun
                    && reply.Marketplace == review.Marketplace
                    && reply.ReviewIdOnMp == review.ReviewIdOnMp
                    && reply.WbProductId == review.WbProductId
                    && reply.SourceWbRootId == review.SourceWbRootId)
            })
            .ToListAsync(cancellationToken);

        var items = pageRows
            .Select(x => MapToListItem(x.Review, x.HasObservedReply))
            .ToList();

        return ServiceResult<PagedResponse<ParserReviewListItemDto>>.Success(
            new PagedResponse<ParserReviewListItemDto>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<ParserReviewDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ParserReviewDetailDto>.BadRequest("Parser review row id is required.");

        var row = await _dbContext.ParserReviewRows
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (row is null)
            return ServiceResult<ParserReviewDetailDto>.NotFound("Parser review row was not found.");

        var hasObservedReply = await HasObservedReplyAsync(row, cancellationToken);
        var sourceFile = await _dbContext.ParserFiles
            .AsNoTracking()
            .FirstAsync(x => x.Id == row.IdParserFile, cancellationToken);
        var rootFetch = row.IdReviewRootFetch.HasValue
            ? await _dbContext.ParserReviewRootFetches
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == row.IdReviewRootFetch.Value, cancellationToken)
            : null;

        return ServiceResult<ParserReviewDetailDto>.Success(
            MapToDetail(row, hasObservedReply, sourceFile, rootFetch));
    }

    public async Task<ServiceResult<PagedResponse<ParserReviewReplyDto>>> GetRepliesAsync(
        Guid id,
        ParserReviewReplyListQuery query,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<PagedResponse<ParserReviewReplyDto>>.BadRequest("Parser review row id is required.");

        var review = await _dbContext.ParserReviewRows
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (review is null)
            return ServiceResult<PagedResponse<ParserReviewReplyDto>>.NotFound("Parser review row was not found.");

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var replies = _dbContext.ParserReviewReplyRows
            .AsNoTracking()
            .Where(reply =>
                reply.IdParserRun == review.IdParserRun
                && reply.Marketplace == review.Marketplace
                && reply.ReviewIdOnMp == review.ReviewIdOnMp
                && reply.WbProductId == review.WbProductId
                && reply.SourceWbRootId == review.SourceWbRootId);

        replies = ApplyReplySort(replies, query.Sort);

        var totalCount = await replies.CountAsync(cancellationToken);
        var replyRows = await replies
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(reply => new
            {
                Reply = reply,
                SourceFileKind = _dbContext.ParserFiles
                    .Where(file => file.Id == reply.IdParserFile)
                    .Select(file => file.Kind)
                    .First()
            })
            .ToListAsync(cancellationToken);

        var items = replyRows
            .Select(x => MapToReply(x.Reply, x.SourceFileKind))
            .ToList();

        return ServiceResult<PagedResponse<ParserReviewReplyDto>>.Success(
            new PagedResponse<ParserReviewReplyDto>(items, page, pageSize, totalCount));
    }

    private IQueryable<ParserReviewRow> ApplyFilters(
        IQueryable<ParserReviewRow> rows,
        ParserReviewListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.ParserRunId))
            rows = rows.Where(x => x.ParserRunId == query.ParserRunId.Trim());

        if (!string.IsNullOrWhiteSpace(query.WbProductId))
            rows = rows.Where(x => x.WbProductId == query.WbProductId.Trim());

        if (!string.IsNullOrWhiteSpace(query.SourceWbRootId))
            rows = rows.Where(x => x.SourceWbRootId == query.SourceWbRootId.Trim());

        if (query.Rating.HasValue)
            rows = rows.Where(x => x.Rating == query.Rating.Value);

        if (query.CappedRootPayload.HasValue)
            rows = rows.Where(x => x.IsCappedRootPayload == query.CappedRootPayload.Value);

        if (!string.IsNullOrWhiteSpace(query.ReviewAttributionMode))
            rows = rows.Where(x => x.ReviewAttributionMode == query.ReviewAttributionMode.Trim());

        if (query.CreatedAtOnMpFrom.HasValue)
            rows = rows.Where(x => x.CreatedAtOnMp >= query.CreatedAtOnMpFrom.Value);

        if (query.CreatedAtOnMpTo.HasValue)
            rows = rows.Where(x => x.CreatedAtOnMp <= query.CreatedAtOnMpTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                (x.Text != null && EF.Functions.ILike(x.Text, $"%{search}%"))
                || (x.Pros != null && EF.Functions.ILike(x.Pros, $"%{search}%"))
                || (x.Cons != null && EF.Functions.ILike(x.Cons, $"%{search}%"))
                || EF.Functions.ILike(x.ReviewIdOnMp, $"%{search}%")
                || EF.Functions.ILike(x.WbProductId, $"%{search}%"));
        }

        if (query.HasObservedReply.HasValue)
        {
            rows = rows.Where(review =>
                _dbContext.ParserReviewReplyRows.Any(reply =>
                    reply.IdParserRun == review.IdParserRun
                    && reply.Marketplace == review.Marketplace
                    && reply.ReviewIdOnMp == review.ReviewIdOnMp
                    && reply.WbProductId == review.WbProductId
                    && reply.SourceWbRootId == review.SourceWbRootId) == query.HasObservedReply.Value);
        }

        return rows;
    }

    private static IOrderedQueryable<ParserReviewRow> ApplySort(
        IQueryable<ParserReviewRow> rows,
        string? sort)
    {
        return sort?.Trim() switch
        {
            "createdAtOnMp" => rows.OrderBy(x => x.CreatedAtOnMp).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "parsedAtUtc" => rows.OrderBy(x => x.ParsedAtUtc).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-parsedAtUtc" => rows.OrderByDescending(x => x.ParsedAtUtc).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            "rating" => rows.OrderBy(x => x.Rating).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-rating" => rows.OrderByDescending(x => x.Rating).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            _ => rows.OrderByDescending(x => x.CreatedAtOnMp).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id)
        };
    }

    private static IOrderedQueryable<ParserReviewReplyRow> ApplyReplySort(
        IQueryable<ParserReviewReplyRow> rows,
        string? sort)
    {
        return sort?.Trim() switch
        {
            "createdAtOnMp" => rows.OrderBy(x => x.CreatedAtOnMp).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "updatedAtOnMp" => rows.OrderBy(x => x.UpdatedAtOnMp).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-updatedAtOnMp" => rows.OrderByDescending(x => x.UpdatedAtOnMp).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            _ => rows.OrderByDescending(x => x.CreatedAtOnMp).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id)
        };
    }

    private async Task<bool> HasObservedReplyAsync(
        ParserReviewRow row,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ParserReviewReplyRows
            .AsNoTracking()
            .AnyAsync(reply =>
                reply.IdParserRun == row.IdParserRun
                && reply.Marketplace == row.Marketplace
                && reply.ReviewIdOnMp == row.ReviewIdOnMp
                && reply.WbProductId == row.WbProductId
                && reply.SourceWbRootId == row.SourceWbRootId,
                cancellationToken);
    }

    private static ParserReviewListItemDto MapToListItem(
        ParserReviewRow row,
        bool hasObservedReply)
    {
        return new ParserReviewListItemDto(
            row.Id,
            row.ParserRunId,
            row.ParsedAtUtc,
            row.SourceWbRootId,
            row.WbProductId,
            row.ReviewIdOnMp,
            row.ReviewAttributionMode,
            row.Rating,
            GetPreview(row.Text),
            row.CreatedAtOnMp,
            hasObservedReply,
            row.IsPartialSnapshot,
            row.IsCappedRootPayload,
            row.IsFullHistoryUnknown);
    }

    private static ParserReviewDetailDto MapToDetail(
        ParserReviewRow row,
        bool hasObservedReply,
        ParserFile sourceFile,
        ParserReviewRootFetch? rootFetch)
    {
        return new ParserReviewDetailDto(
            row.Id,
            row.ParserRunId,
            row.ParsedAtUtc,
            row.SourceWbRootId,
            row.WbProductId,
            row.ReviewIdOnMp,
            row.ReviewAttributionMode,
            row.Rating,
            GetPreview(row.Text),
            row.CreatedAtOnMp,
            hasObservedReply,
            row.IsPartialSnapshot,
            row.IsCappedRootPayload,
            row.IsFullHistoryUnknown,
            row.Text,
            row.Pros,
            row.Cons,
            row.ReviewerName,
            row.ReviewerCountry,
            row.ReviewerHasPhoto,
            row.HelpfulPlus,
            row.HelpfulMinus,
            row.SourceCategory,
            row.SourceSubcategory,
            row.SourceQuery,
            row.SourceRegionDest,
            row.InputProductsParserRunId,
            sourceFile.Kind,
            sourceFile.Sha256,
            row.SourceLineNumber,
            row.RowHash,
            rootFetch is null
                ? null
                : new ParserReviewRootFetchSummaryDto(
                    rootFetch.Status,
                    rootFetch.TimestampUtc,
                    rootFetch.PayloadFeedbackCount,
                    rootFetch.PayloadFeedbackRowsSeen,
                    rootFetch.SelectedReviewRowsSeen,
                    rootFetch.ReviewsWritten,
                    rootFetch.RepliesWritten));
    }

    private static ParserReviewReplyDto MapToReply(
        ParserReviewReplyRow row,
        string sourceFileKind)
    {
        return new ParserReviewReplyDto(
            row.Id,
            row.ParserRunId,
            row.SourceWbRootId,
            row.WbProductId,
            row.ReviewIdOnMp,
            row.ReviewAttributionMode,
            row.ReplyIdOnMp,
            row.ReplyFallbackHash,
            !string.IsNullOrWhiteSpace(row.ReplyIdOnMp),
            row.Text,
            row.CreatedAtOnMp,
            row.UpdatedAtOnMp,
            row.ReplyAuthor,
            row.ReplyState,
            row.IsPartialSnapshot,
            row.IsCappedRootPayload,
            row.IsFullHistoryUnknown,
            sourceFileKind,
            row.SourceLineNumber);
    }

    private static string? GetPreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var preview = value.Trim();
        return preview.Length <= 220
            ? preview
            : $"{preview[..217]}...";
    }
}
