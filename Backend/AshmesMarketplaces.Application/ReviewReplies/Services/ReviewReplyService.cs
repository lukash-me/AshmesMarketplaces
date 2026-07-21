using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ReviewReplies.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Reviews;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ReviewReplies.Services;

public sealed class ReviewReplyService : IReviewReplyService
{
    private readonly ApplicationDbContext _dbContext;

    public ReviewReplyService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ReviewReplyListItemResponse>>> GetListAsync(ReviewReplyListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var replies = _dbContext.ReviewReplies.AsNoTracking();

        if (query.IdReview.HasValue)
            replies = replies.Where(x => x.IdReview == query.IdReview.Value);

        if (query.Status.HasValue)
            replies = replies.Where(x => x.Status == query.Status.Value);

        if (query.DateCreateFrom.HasValue)
            replies = replies.Where(x => x.DateCreate >= query.DateCreateFrom.Value);

        if (query.DateCreateTo.HasValue)
            replies = replies.Where(x => x.DateCreate <= query.DateCreateTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            replies = replies.Where(x => EF.Functions.ILike(x.IdOnMp, $"%{search}%") || EF.Functions.ILike(x.Text, $"%{search}%"));
        }

        replies = query.Sort?.Trim() switch
        {
            "dateCreate" => replies.OrderBy(x => x.DateCreate),
            "-dateCreate" => replies.OrderByDescending(x => x.DateCreate),
            "dateUpdate" => replies.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => replies.OrderByDescending(x => x.DateUpdate),
            _ => replies.OrderByDescending(x => x.DateCreate)
        };

        var totalCount = await replies.CountAsync(cancellationToken);
        var items = await replies
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ReviewReplyListItemResponse(x.Id, x.IdReview, x.IdOnMp, x.Text, x.Status, x.DateCreate, x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<ReviewReplyListItemResponse>>.Success(new PagedResponse<ReviewReplyListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<ReviewReplyResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ReviewReplyResponse>.BadRequest("Review reply id is required.");

        var reply = await _dbContext.ReviewReplies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return reply is null
            ? ServiceResult<ReviewReplyResponse>.NotFound("Review reply was not found.")
            : ServiceResult<ReviewReplyResponse>.Success(MapToResponse(reply));
    }

    public async Task<ServiceResult<ReviewReplyResponse>> CreateAsync(CreateReviewReplyRequest request, CancellationToken cancellationToken)
    {
        if (!await ReviewExistsAsync(request.IdReview, cancellationToken))
            return ServiceResult<ReviewReplyResponse>.Conflict("Review was not found.");

        try
        {
            var reply = new ReviewReply(request.IdReview, request.IdOnMp, request.Text, request.Status, request.DateCreate, request.DateUpdate);
            _dbContext.ReviewReplies.Add(reply);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ReviewReplyResponse>.Success(MapToResponse(reply));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<ReviewReplyResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ReviewReplyResponse>.Conflict("Review reply cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<ReviewReplyResponse>> UpdateAsync(Guid id, UpdateReviewReplyRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ReviewReplyResponse>.BadRequest("Review reply id is required.");

        var reply = await _dbContext.ReviewReplies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reply is null)
            return ServiceResult<ReviewReplyResponse>.NotFound("Review reply was not found.");

        if (!await ReviewExistsAsync(request.IdReview, cancellationToken))
            return ServiceResult<ReviewReplyResponse>.Conflict("Review was not found.");

        try
        {
            _ = new ReviewReply(request.IdReview, request.IdOnMp, request.Text, request.Status, request.DateCreate, request.DateUpdate);

            var entry = _dbContext.Entry(reply);
            entry.Property(x => x.IdReview).CurrentValue = request.IdReview;
            entry.Property(x => x.IdOnMp).CurrentValue = request.IdOnMp;
            entry.Property(x => x.Text).CurrentValue = request.Text;
            entry.Property(x => x.Status).CurrentValue = request.Status;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ReviewReplyResponse>.Success(MapToResponse(reply));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<ReviewReplyResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ReviewReplyResponse>.Conflict("Review reply cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Review reply id is required.");

        var reply = await _dbContext.ReviewReplies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reply is null)
            return ServiceResult.NotFound("Review reply was not found.");

        _dbContext.ReviewReplies.Remove(reply);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Review reply cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<bool> ReviewExistsAsync(Guid idReview, CancellationToken cancellationToken)
    {
        return await _dbContext.Reviews.AsNoTracking().AnyAsync(x => x.Id == idReview, cancellationToken);
    }

    private static ReviewReplyResponse MapToResponse(ReviewReply reply)
    {
        return new ReviewReplyResponse(reply.Id, reply.IdReview, reply.IdOnMp, reply.Text, reply.Status, reply.DateCreate, reply.DateUpdate);
    }
}
