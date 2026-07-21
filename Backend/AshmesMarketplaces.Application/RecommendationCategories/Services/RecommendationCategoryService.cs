using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RecommendationCategories.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.RecommendationCategories.Services;

public sealed class RecommendationCategoryService : IRecommendationCategoryService
{
    private readonly ApplicationDbContext _dbContext;

    public RecommendationCategoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<RecommendationCategoryListItemResponse>>> GetListAsync(RecommendationCategoryListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var recommendationCategories = _dbContext.RecommendationCategories.AsNoTracking();

        if (query.IdRecommendation.HasValue)
            recommendationCategories = recommendationCategories.Where(x => x.IdRecommendation == query.IdRecommendation.Value);

        if (query.IdCategory.HasValue)
            recommendationCategories = recommendationCategories.Where(x => x.IdCategory == query.IdCategory.Value);

        recommendationCategories = query.Sort?.Trim() switch
        {
            "idRecommendation" => recommendationCategories.OrderBy(x => x.IdRecommendation).ThenBy(x => x.IdCategory),
            "-idRecommendation" => recommendationCategories.OrderByDescending(x => x.IdRecommendation).ThenBy(x => x.IdCategory),
            "idCategory" => recommendationCategories.OrderBy(x => x.IdCategory).ThenBy(x => x.IdRecommendation),
            "-idCategory" => recommendationCategories.OrderByDescending(x => x.IdCategory).ThenBy(x => x.IdRecommendation),
            _ => recommendationCategories.OrderBy(x => x.IdRecommendation).ThenBy(x => x.IdCategory)
        };

        var totalCount = await recommendationCategories.CountAsync(cancellationToken);
        var items = await recommendationCategories
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RecommendationCategoryListItemResponse(
                x.IdRecommendation,
                x.IdCategory))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<RecommendationCategoryListItemResponse>>.Success(new PagedResponse<RecommendationCategoryListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<RecommendationCategoryResponse>> GetByIdAsync(Guid idRecommendation, Guid idCategory, CancellationToken cancellationToken)
    {
        if (idRecommendation == Guid.Empty)
            return ServiceResult<RecommendationCategoryResponse>.BadRequest("Recommendation id is required.");

        if (idCategory == Guid.Empty)
            return ServiceResult<RecommendationCategoryResponse>.BadRequest("Category id is required.");

        var recommendationCategory = await _dbContext.RecommendationCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdRecommendation == idRecommendation && x.IdCategory == idCategory, cancellationToken);

        return recommendationCategory is null
            ? ServiceResult<RecommendationCategoryResponse>.NotFound("Recommendation category was not found.")
            : ServiceResult<RecommendationCategoryResponse>.Success(MapToResponse(recommendationCategory));
    }

    public async Task<ServiceResult<RecommendationCategoryResponse>> CreateAsync(CreateRecommendationCategoryRequest request, CancellationToken cancellationToken)
    {
        var referenceCheck = await ValidateReferencesAsync(request.IdRecommendation, request.IdCategory, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<RecommendationCategoryResponse>.Conflict(referenceCheck);

        try
        {
            var recommendationCategory = new RecommendationCategory(
                request.IdRecommendation,
                request.IdCategory);

            _dbContext.RecommendationCategories.Add(recommendationCategory);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RecommendationCategoryResponse>.Success(MapToResponse(recommendationCategory));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RecommendationCategoryResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RecommendationCategoryResponse>.Conflict("Recommendation category cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid idRecommendation, Guid idCategory, CancellationToken cancellationToken)
    {
        if (idRecommendation == Guid.Empty)
            return ServiceResult.BadRequest("Recommendation id is required.");

        if (idCategory == Guid.Empty)
            return ServiceResult.BadRequest("Category id is required.");

        var recommendationCategory = await _dbContext.RecommendationCategories
            .FirstOrDefaultAsync(x => x.IdRecommendation == idRecommendation && x.IdCategory == idCategory, cancellationToken);

        if (recommendationCategory is null)
            return ServiceResult.NotFound("Recommendation category was not found.");

        _dbContext.RecommendationCategories.Remove(recommendationCategory);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Recommendation category cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<string?> ValidateReferencesAsync(Guid idRecommendation, Guid idCategory, CancellationToken cancellationToken)
    {
        var recommendationExists = await _dbContext.Recommendations.AsNoTracking().AnyAsync(x => x.Id == idRecommendation, cancellationToken);
        if (!recommendationExists)
            return "Recommendation was not found.";

        var categoryExists = await _dbContext.Categories.AsNoTracking().AnyAsync(x => x.Id == idCategory, cancellationToken);
        if (!categoryExists)
            return "Category was not found.";

        return null;
    }

    private static RecommendationCategoryResponse MapToResponse(RecommendationCategory recommendationCategory)
    {
        return new RecommendationCategoryResponse(
            recommendationCategory.IdRecommendation,
            recommendationCategory.IdCategory);
    }
}
