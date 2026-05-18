using System.Text.Json;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Recommendations.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Recommendations.Services;

public sealed class RecommendationService : IRecommendationService
{
    private readonly ApplicationDbContext _dbContext;

    public RecommendationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<RecommendationListItemResponse>>> GetListAsync(RecommendationListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var recommendations = _dbContext.Recommendations.AsNoTracking();

        if (query.IdModel.HasValue)
            recommendations = recommendations.Where(x => x.IdModel == query.IdModel.Value);

        if (query.Type.HasValue)
            recommendations = recommendations.Where(x => x.Type == query.Type.Value);

        if (query.TypeObject.HasValue)
            recommendations = recommendations.Where(x => x.TypeObject == query.TypeObject.Value);

        if (query.DateCreateFrom.HasValue)
            recommendations = recommendations.Where(x => x.DateCreate >= query.DateCreateFrom.Value);

        if (query.DateCreateTo.HasValue)
            recommendations = recommendations.Where(x => x.DateCreate <= query.DateCreateTo.Value);

        recommendations = query.Sort?.Trim() switch
        {
            "score" => recommendations.OrderBy(x => x.Score),
            "-score" => recommendations.OrderByDescending(x => x.Score),
            "dateCreate" => recommendations.OrderBy(x => x.DateCreate),
            "-dateCreate" => recommendations.OrderByDescending(x => x.DateCreate),
            "dateUpdate" => recommendations.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => recommendations.OrderByDescending(x => x.DateUpdate),
            _ => recommendations.OrderByDescending(x => x.DateCreate)
        };

        var totalCount = await recommendations.CountAsync(cancellationToken);
        var items = await recommendations
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RecommendationListItemResponse(
                x.Id,
                x.IdModel,
                x.Type,
                x.TypeObject,
                x.Score,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<RecommendationListItemResponse>>.Success(new PagedResponse<RecommendationListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<RecommendationResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<RecommendationResponse>.BadRequest("Recommendation id is required.");

        var recommendation = await _dbContext.Recommendations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return recommendation is null
            ? ServiceResult<RecommendationResponse>.NotFound("Recommendation was not found.")
            : ServiceResult<RecommendationResponse>.Success(MapToResponse(recommendation));
    }

    public async Task<ServiceResult<RecommendationResponse>> CreateAsync(CreateRecommendationRequest request, CancellationToken cancellationToken)
    {
        JsonDocument? explanation = null;
        JsonDocument? snapshot = null;

        try
        {
            _ = new Recommendation(
                request.IdModel,
                request.Type,
                request.TypeObject,
                request.Score,
                null,
                null,
                request.DateCreate,
                request.DateUpdate);

            explanation = CloneJsonDocument(request.Explanation);
            snapshot = CloneJsonDocument(request.Snapshot);

            var recommendation = new Recommendation(
                request.IdModel,
                request.Type,
                request.TypeObject,
                request.Score,
                explanation,
                snapshot,
                request.DateCreate,
                request.DateUpdate);

            _dbContext.Recommendations.Add(recommendation);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RecommendationResponse>.Success(MapToResponse(recommendation));
        }
        catch (JsonException exception)
        {
            explanation?.Dispose();
            snapshot?.Dispose();
            return ServiceResult<RecommendationResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            explanation?.Dispose();
            snapshot?.Dispose();
            return ServiceResult<RecommendationResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            explanation?.Dispose();
            snapshot?.Dispose();
            return ServiceResult<RecommendationResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RecommendationResponse>.Conflict("Recommendation cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<RecommendationResponse>> UpdateAsync(Guid id, UpdateRecommendationRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<RecommendationResponse>.BadRequest("Recommendation id is required.");

        var recommendation = await _dbContext.Recommendations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (recommendation is null)
            return ServiceResult<RecommendationResponse>.NotFound("Recommendation was not found.");

        JsonDocument? explanation = null;
        JsonDocument? snapshot = null;

        try
        {
            _ = new Recommendation(
                request.IdModel,
                request.Type,
                request.TypeObject,
                request.Score,
                null,
                null,
                request.DateCreate,
                request.DateUpdate);

            explanation = CloneJsonDocument(request.Explanation);
            snapshot = CloneJsonDocument(request.Snapshot);

            var oldExplanation = recommendation.Explanation;
            var oldSnapshot = recommendation.Snapshot;
            var entry = _dbContext.Entry(recommendation);
            entry.Property(x => x.IdModel).CurrentValue = request.IdModel;
            entry.Property(x => x.Type).CurrentValue = request.Type;
            entry.Property(x => x.TypeObject).CurrentValue = request.TypeObject;
            entry.Property(x => x.Score).CurrentValue = request.Score;
            entry.Property(x => x.Explanation).CurrentValue = explanation;
            entry.Property(x => x.Snapshot).CurrentValue = snapshot;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            oldExplanation?.Dispose();
            oldSnapshot?.Dispose();

            return ServiceResult<RecommendationResponse>.Success(MapToResponse(recommendation));
        }
        catch (JsonException exception)
        {
            explanation?.Dispose();
            snapshot?.Dispose();
            return ServiceResult<RecommendationResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            explanation?.Dispose();
            snapshot?.Dispose();
            return ServiceResult<RecommendationResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            explanation?.Dispose();
            snapshot?.Dispose();
            return ServiceResult<RecommendationResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<RecommendationResponse>.Conflict("Recommendation cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Recommendation id is required.");

        var recommendation = await _dbContext.Recommendations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (recommendation is null)
            return ServiceResult.NotFound("Recommendation was not found.");

        _dbContext.Recommendations.Remove(recommendation);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            recommendation.Dispose();
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Recommendation cannot be deleted because it is referenced by other records.");
        }
    }

    private static RecommendationResponse MapToResponse(Recommendation recommendation)
    {
        return new RecommendationResponse(
            recommendation.Id,
            recommendation.IdModel,
            recommendation.Type,
            recommendation.TypeObject,
            recommendation.Score,
            CloneJsonElement(recommendation.Explanation),
            CloneJsonElement(recommendation.Snapshot),
            recommendation.DateCreate,
            recommendation.DateUpdate);
    }

    private static JsonElement? CloneJsonElement(JsonDocument? document)
    {
        return document is null
            ? null
            : document.RootElement.Clone();
    }

    private static JsonDocument? CloneJsonDocument(JsonElement? element)
    {
        return element.HasValue
            ? JsonDocument.Parse(element.Value.GetRawText())
            : null;
    }
}
