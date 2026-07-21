using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RecommendationProducts.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Recommendations;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.RecommendationProducts.Services;

public sealed class RecommendationProductService : IRecommendationProductService
{
    private readonly ApplicationDbContext _dbContext;

    public RecommendationProductService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<RecommendationProductListItemResponse>>> GetListAsync(RecommendationProductListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var recommendationProducts = _dbContext.RecommendationProducts.AsNoTracking();

        if (query.IdRecommendation.HasValue)
            recommendationProducts = recommendationProducts.Where(x => x.IdRecommendation == query.IdRecommendation.Value);

        if (query.IdProduct.HasValue)
            recommendationProducts = recommendationProducts.Where(x => x.IdProduct == ProductId.Create(query.IdProduct.Value));

        recommendationProducts = query.Sort?.Trim() switch
        {
            "idRecommendation" => recommendationProducts.OrderBy(x => x.IdRecommendation).ThenBy(x => x.IdProduct),
            "-idRecommendation" => recommendationProducts.OrderByDescending(x => x.IdRecommendation).ThenBy(x => x.IdProduct),
            "idProduct" => recommendationProducts.OrderBy(x => x.IdProduct).ThenBy(x => x.IdRecommendation),
            "-idProduct" => recommendationProducts.OrderByDescending(x => x.IdProduct).ThenBy(x => x.IdRecommendation),
            _ => recommendationProducts.OrderBy(x => x.IdRecommendation).ThenBy(x => x.IdProduct)
        };

        var totalCount = await recommendationProducts.CountAsync(cancellationToken);
        var items = await recommendationProducts
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RecommendationProductListItemResponse(
                x.IdRecommendation,
                x.IdProduct.Value))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<RecommendationProductListItemResponse>>.Success(new PagedResponse<RecommendationProductListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<RecommendationProductResponse>> GetByIdAsync(Guid idRecommendation, Guid idProduct, CancellationToken cancellationToken)
    {
        if (idRecommendation == Guid.Empty)
            return ServiceResult<RecommendationProductResponse>.BadRequest("Recommendation id is required.");

        if (idProduct == Guid.Empty)
            return ServiceResult<RecommendationProductResponse>.BadRequest("Product id is required.");

        var productId = ProductId.Create(idProduct);
        var recommendationProduct = await _dbContext.RecommendationProducts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdRecommendation == idRecommendation && x.IdProduct == productId, cancellationToken);

        return recommendationProduct is null
            ? ServiceResult<RecommendationProductResponse>.NotFound("Recommendation product was not found.")
            : ServiceResult<RecommendationProductResponse>.Success(MapToResponse(recommendationProduct));
    }

    public async Task<ServiceResult<RecommendationProductResponse>> CreateAsync(CreateRecommendationProductRequest request, CancellationToken cancellationToken)
    {
        var referenceCheck = await ValidateReferencesAsync(request.IdRecommendation, request.IdProduct, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<RecommendationProductResponse>.Conflict(referenceCheck);

        try
        {
            var recommendationProduct = new RecommendationProduct(
                request.IdRecommendation,
                ProductId.Create(request.IdProduct));

            _dbContext.RecommendationProducts.Add(recommendationProduct);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RecommendationProductResponse>.Success(MapToResponse(recommendationProduct));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<RecommendationProductResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RecommendationProductResponse>.Conflict("Recommendation product cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid idRecommendation, Guid idProduct, CancellationToken cancellationToken)
    {
        if (idRecommendation == Guid.Empty)
            return ServiceResult.BadRequest("Recommendation id is required.");

        if (idProduct == Guid.Empty)
            return ServiceResult.BadRequest("Product id is required.");

        var productId = ProductId.Create(idProduct);
        var recommendationProduct = await _dbContext.RecommendationProducts
            .FirstOrDefaultAsync(x => x.IdRecommendation == idRecommendation && x.IdProduct == productId, cancellationToken);

        if (recommendationProduct is null)
            return ServiceResult.NotFound("Recommendation product was not found.");

        _dbContext.RecommendationProducts.Remove(recommendationProduct);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Recommendation product cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<string?> ValidateReferencesAsync(Guid idRecommendation, Guid idProduct, CancellationToken cancellationToken)
    {
        var recommendationExists = await _dbContext.Recommendations.AsNoTracking().AnyAsync(x => x.Id == idRecommendation, cancellationToken);
        if (!recommendationExists)
            return "Recommendation was not found.";

        var productId = ProductId.Create(idProduct);
        var productExists = await _dbContext.Products.AsNoTracking().AnyAsync(x => x.Id == productId, cancellationToken);
        if (!productExists)
            return "Product was not found.";

        return null;
    }

    private static RecommendationProductResponse MapToResponse(RecommendationProduct recommendationProduct)
    {
        return new RecommendationProductResponse(
            recommendationProduct.IdRecommendation,
            recommendationProduct.IdProduct.Value);
    }
}
