using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Reviews.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Reviews;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Reviews.Services;

public sealed class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _dbContext;

    public ReviewService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ReviewListItemResponse>>> GetListAsync(ReviewListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var reviews = _dbContext.Reviews.AsNoTracking();

        if (query.IdProduct.HasValue)
            reviews = reviews.Where(x => x.IdProduct == ProductId.Create(query.IdProduct.Value));

        if (query.IsReplied.HasValue)
            reviews = reviews.Where(x => x.IsReplied == query.IsReplied.Value);

        if (query.Rating.HasValue)
            reviews = reviews.Where(x => x.Rating == query.Rating.Value);

        if (query.DateCreateFrom.HasValue)
            reviews = reviews.Where(x => x.DateCreate >= query.DateCreateFrom.Value);

        if (query.DateCreateTo.HasValue)
            reviews = reviews.Where(x => x.DateCreate <= query.DateCreateTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            reviews = reviews.Where(x =>
                EF.Functions.ILike(x.IdOnMp, $"%{search}%")
                || (x.Text != null && EF.Functions.ILike(x.Text, $"%{search}%")));
        }

        reviews = query.Sort?.Trim() switch
        {
            "dateCreate" => reviews.OrderBy(x => x.DateCreate),
            "-dateCreate" => reviews.OrderByDescending(x => x.DateCreate),
            "rating" => reviews.OrderBy(x => x.Rating),
            "-rating" => reviews.OrderByDescending(x => x.Rating),
            _ => reviews.OrderByDescending(x => x.DateCreate)
        };

        var totalCount = await reviews.CountAsync(cancellationToken);
        var items = await reviews
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ReviewListItemResponse(x.Id, x.IdProduct.Value, x.IdOnMp, x.Rating, x.Text, x.IsReplied, x.DateCreate, x.DateReply))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<ReviewListItemResponse>>.Success(new PagedResponse<ReviewListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<ReviewResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ReviewResponse>.BadRequest("Review id is required.");

        var review = await _dbContext.Reviews.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return review is null
            ? ServiceResult<ReviewResponse>.NotFound("Review was not found.")
            : ServiceResult<ReviewResponse>.Success(MapToResponse(review));
    }

    public async Task<ServiceResult<ReviewResponse>> CreateAsync(CreateReviewRequest request, CancellationToken cancellationToken)
    {
        if (!await ProductExistsAsync(request.IdProduct, cancellationToken))
            return ServiceResult<ReviewResponse>.Conflict("Product was not found.");

        try
        {
            var review = new Review(
                ProductId.Create(request.IdProduct),
                request.IdOnMp,
                request.Rating,
                request.Text,
                request.IsReplied,
                request.DateCreate,
                request.DateReply);

            _dbContext.Reviews.Add(review);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ReviewResponse>.Success(MapToResponse(review));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<ReviewResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<ReviewResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ReviewResponse>.Conflict("Review cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<ReviewResponse>> UpdateAsync(Guid id, UpdateReviewRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ReviewResponse>.BadRequest("Review id is required.");

        var review = await _dbContext.Reviews.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (review is null)
            return ServiceResult<ReviewResponse>.NotFound("Review was not found.");

        if (!await ProductExistsAsync(request.IdProduct, cancellationToken))
            return ServiceResult<ReviewResponse>.Conflict("Product was not found.");

        try
        {
            _ = new Review(ProductId.Create(request.IdProduct), request.IdOnMp, request.Rating, request.Text, request.IsReplied, request.DateCreate, request.DateReply);

            var entry = _dbContext.Entry(review);
            entry.Property(x => x.IdProduct).CurrentValue = ProductId.Create(request.IdProduct);
            entry.Property(x => x.IdOnMp).CurrentValue = request.IdOnMp;
            entry.Property(x => x.Rating).CurrentValue = request.Rating;
            entry.Property(x => x.Text).CurrentValue = request.Text;
            entry.Property(x => x.IsReplied).CurrentValue = request.IsReplied;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateReply).CurrentValue = request.DateReply;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ReviewResponse>.Success(MapToResponse(review));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<ReviewResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<ReviewResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ReviewResponse>.Conflict("Review cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Review id is required.");

        var review = await _dbContext.Reviews.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (review is null)
            return ServiceResult.NotFound("Review was not found.");

        _dbContext.Reviews.Remove(review);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Review cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<bool> ProductExistsAsync(Guid idProduct, CancellationToken cancellationToken)
    {
        return await _dbContext.Products.AsNoTracking().AnyAsync(x => x.Id == ProductId.Create(idProduct), cancellationToken);
    }

    private static ReviewResponse MapToResponse(Review review)
    {
        return new ReviewResponse(review.Id, review.IdProduct.Value, review.IdOnMp, review.Rating, review.Text, review.IsReplied, review.DateCreate, review.DateReply);
    }
}
