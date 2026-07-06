using System.Text.Json;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Application.ParserIngestion.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserRunRollbackService : IParserRunRollbackService
{
    private const string AdminRoleName = "Admin";
    private const string NoDataMessage = "Новых данных получено не было, откатывать нечего";
    private const string MissingLedgerMessage = "Для этого запуска нет сохраненного журнала эффектов для безопасного отката.";
    private const string AlreadyRolledBackMessage = "Откат для этого запуска уже выполнен.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ParserRunRollbackService(ApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<ParserRunRollbackPreviewDto>> PreviewAsync(Guid parserProxyRunId, CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<ParserRunRollbackPreviewDto>(access.Error!);

        var preview = await BuildPreviewAsync(parserProxyRunId, cancellationToken);
        return preview is null
            ? ServiceResult<ParserRunRollbackPreviewDto>.NotFound("Parser journal row was not found.")
            : ServiceResult<ParserRunRollbackPreviewDto>.Success(preview);
    }

    public async Task<ServiceResult<ParserRunRollbackResponseDto>> RollbackAsync(Guid parserProxyRunId, CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<ParserRunRollbackResponseDto>(access.Error!);

        var preview = await BuildPreviewAsync(parserProxyRunId, cancellationToken);
        if (preview is null)
            return ServiceResult<ParserRunRollbackResponseDto>.NotFound("Parser journal row was not found.");
        if (!preview.CanRollback)
        {
            return preview.ConflictProductsCount > 0
                ? ServiceResult<ParserRunRollbackResponseDto>.Conflict(preview.Message)
                : ServiceResult<ParserRunRollbackResponseDto>.BadRequest(preview.Message);
        }

        if (_dbContext.Database.IsRelational())
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var result = await RollbackInternalAsync(parserProxyRunId, preview, cancellationToken);
            if (result.IsSuccess)
                await transaction.CommitAsync(cancellationToken);

            return result;
        }

        return await RollbackInternalAsync(parserProxyRunId, preview, cancellationToken);
    }

    private async Task<ServiceResult<ParserRunRollbackResponseDto>> RollbackInternalAsync(
        Guid parserProxyRunId,
        ParserRunRollbackPreviewDto preview,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rollback = new ParserRunRollback(parserProxyRunId, _currentUser.UserId, now);
        _dbContext.ParserRunRollbacks.Add(rollback);

        var run = await _dbContext.ParserProxyRuns
            .FirstAsync(x => x.Id == parserProxyRunId, cancellationToken);
        var productEffects = await _dbContext.ParserRunProductEffects
            .Where(x => x.ParserProxyRunId == parserProxyRunId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var entityEffects = await _dbContext.ParserRunCurrentEntityEffects
            .Where(x => x.ParserProxyRunId == parserProxyRunId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var createdProductIds = productEffects
            .Where(x => x.EffectType == ParserRunProductEffectTypes.Created)
            .Select(x => x.WbProductId)
            .ToHashSet(StringComparer.Ordinal);
        var deletedProductsCount = await DeleteCreatedProductsAsync(createdProductIds, cancellationToken);
        var restoredProducts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var effect in entityEffects)
        {
            if (createdProductIds.Contains(effect.WbProductId))
                continue;

            if (effect.EffectType == ParserRunCurrentEntityEffectTypes.Created)
            {
                await DeleteCreatedEntityAsync(effect, cancellationToken);
                effect.MarkRolledBack(rollback.Id, now);
                continue;
            }

            if (effect.EffectType == ParserRunCurrentEntityEffectTypes.Updated && !string.IsNullOrWhiteSpace(effect.OldValueJson))
            {
                await RestoreEntityAsync(effect, now, cancellationToken);
                restoredProducts.Add(effect.WbProductId);
                effect.MarkRolledBack(rollback.Id, now);
            }
        }

        foreach (var effect in productEffects)
            effect.MarkRolledBack(rollback.Id, now);

        foreach (var wbProductId in productEffects
                     .Select(x => x.WbProductId)
                     .Union(entityEffects.Select(x => x.WbProductId), StringComparer.Ordinal)
                     .Distinct(StringComparer.Ordinal))
        {
            _dbContext.ParserProductChangeEvents.Add(new ParserProductChangeEvent(
                wbProductId,
                null,
                $"rollback:{rollback.Id:N}",
                run.SourceCategory,
                run.SourceSubcategory,
                "rollback",
                "rollback",
                null,
                $"rollback:{rollback.Id:N}:{wbProductId}",
                null,
                JsonSerializer.Serialize(new { parserProxyRunId, rollbackId = rollback.Id }, JsonOptions),
                now));
        }

        rollback.Complete(
            preview.CreatedProductsCount,
            preview.UpdatedProductsCount,
            deletedProductsCount,
            restoredProducts.Count,
            0,
            "Откат выполнен.",
            now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ParserRunRollbackResponseDto>.Success(new ParserRunRollbackResponseDto(
            rollback.Id,
            parserProxyRunId,
            rollback.Status,
            rollback.Message ?? "Откат выполнен.",
            rollback.CreatedProductsCount,
            rollback.UpdatedProductsCount,
            rollback.DeletedProductsCount,
            rollback.RestoredProductsCount,
            rollback.ConflictProductsCount));
    }

    private async Task<ParserRunRollbackPreviewDto?> BuildPreviewAsync(Guid parserProxyRunId, CancellationToken cancellationToken)
    {
        var run = await _dbContext.ParserProxyRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == parserProxyRunId, cancellationToken);
        if (run is null)
            return null;

        if (run.DownloadedProductsCount <= 0)
        {
            return new ParserRunRollbackPreviewDto(
                parserProxyRunId,
                false,
                NoDataMessage,
                0,
                0,
                0,
                0);
        }

        var productEffects = await _dbContext.ParserRunProductEffects
            .AsNoTracking()
            .Where(x => x.ParserProxyRunId == parserProxyRunId)
            .ToListAsync(cancellationToken);
        var entityEffects = await _dbContext.ParserRunCurrentEntityEffects
            .AsNoTracking()
            .Where(x => x.ParserProxyRunId == parserProxyRunId)
            .ToListAsync(cancellationToken);

        if (productEffects.Count == 0 && entityEffects.Count == 0)
        {
            return new ParserRunRollbackPreviewDto(
                parserProxyRunId,
                false,
                MissingLedgerMessage,
                0,
                0,
                0,
                0);
        }

        var completedRollbackExists = await _dbContext.ParserRunRollbacks
            .AsNoTracking()
            .AnyAsync(
                x => x.ParserProxyRunId == parserProxyRunId &&
                     x.Status == ParserRunRollbackStatuses.Completed,
                cancellationToken);
        if (completedRollbackExists)
        {
            return new ParserRunRollbackPreviewDto(
                parserProxyRunId,
                false,
                AlreadyRolledBackMessage,
                0,
                0,
                0,
                productEffects.Count(x => x.RollbackStatus == ParserRunEffectRollbackStatuses.RolledBack));
        }

        var affectedProductIds = productEffects
            .Select(x => x.WbProductId)
            .Union(entityEffects.Select(x => x.WbProductId), StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var latestProductEffectAt = productEffects.Count == 0 ? run.StartedAtUtc : productEffects.Max(x => x.CreatedAtUtc);
        var latestEntityEffectAt = entityEffects.Count == 0 ? run.StartedAtUtc : entityEffects.Max(x => x.CreatedAtUtc);
        var latestEffectAt = latestProductEffectAt > latestEntityEffectAt ? latestProductEffectAt : latestEntityEffectAt;
        var conflictProductIds = new HashSet<string>(StringComparer.Ordinal);
        if (affectedProductIds.Count > 0)
        {
            var productConflicts = await _dbContext.ParserRunProductEffects
                .AsNoTracking()
                .Where(x =>
                    x.ParserProxyRunId != parserProxyRunId &&
                    affectedProductIds.Contains(x.WbProductId) &&
                    x.CreatedAtUtc > latestEffectAt &&
                    x.RollbackStatus != ParserRunEffectRollbackStatuses.RolledBack)
                .Select(x => x.WbProductId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var entityConflicts = await _dbContext.ParserRunCurrentEntityEffects
                .AsNoTracking()
                .Where(x =>
                    x.ParserProxyRunId != parserProxyRunId &&
                    affectedProductIds.Contains(x.WbProductId) &&
                    x.CreatedAtUtc > latestEffectAt &&
                    x.RollbackStatus != ParserRunEffectRollbackStatuses.RolledBack)
                .Select(x => x.WbProductId)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var productId in productConflicts.Concat(entityConflicts))
                conflictProductIds.Add(productId);
        }

        var conflictProductsCount = conflictProductIds.Count;
        var createdProductIds = productEffects
            .Where(x => x.EffectType == ParserRunProductEffectTypes.Created)
            .Select(x => x.WbProductId)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var updatedProductsCount = affectedProductIds.Count(x => !createdProductIds.Contains(x));
        var alreadyRolledBackCount = productEffects.Count(x => x.RollbackStatus == ParserRunEffectRollbackStatuses.RolledBack) +
                                     entityEffects.Count(x => x.RollbackStatus == ParserRunEffectRollbackStatuses.RolledBack);
        if (conflictProductsCount > 0)
        {
            return new ParserRunRollbackPreviewDto(
                parserProxyRunId,
                false,
                $"Откат заблокирован: более свежие запуски изменили карточек: {conflictProductsCount}.",
                createdProductIds.Count,
                updatedProductsCount,
                conflictProductsCount,
                alreadyRolledBackCount);
        }

        return new ParserRunRollbackPreviewDto(
            parserProxyRunId,
            true,
            "Откат доступен.",
            createdProductIds.Count,
            updatedProductsCount,
            0,
            alreadyRolledBackCount);
    }

    private async Task<int> DeleteCreatedProductsAsync(HashSet<string> wbProductIds, CancellationToken cancellationToken)
    {
        if (wbProductIds.Count == 0)
            return 0;

        var products = await _dbContext.ParserCurrentProductRows
            .Where(x => wbProductIds.Contains(x.WbProductId))
            .ToListAsync(cancellationToken);
        var productRowIds = products.Select(x => x.ProductRowId).ToList();
        var productRows = productRowIds.Count == 0
            ? []
            : await _dbContext.ParserProductRows
                .Where(x => productRowIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
        var details = await _dbContext.ParserCurrentProductDetails
            .Where(x => wbProductIds.Contains(x.WbProductId))
            .ToListAsync(cancellationToken);
        var logistics = await _dbContext.ParserCurrentProductLogistics
            .Where(x => wbProductIds.Contains(x.WbProductId))
            .ToListAsync(cancellationToken);
        var ranks = await _dbContext.ParserCurrentProductRanks
            .Where(x => wbProductIds.Contains(x.WbProductId))
            .ToListAsync(cancellationToken);
        var reviewSummaries = await _dbContext.ParserCurrentProductReviewsSummaries
            .Where(x => wbProductIds.Contains(x.WbProductId))
            .ToListAsync(cancellationToken);
        var reviewEvidence = await _dbContext.ParserCurrentProductReviewEvidence
            .Where(x => wbProductIds.Contains(x.WbProductId))
            .ToListAsync(cancellationToken);

        _dbContext.ParserCurrentProductDetails.RemoveRange(details);
        _dbContext.ParserCurrentProductLogistics.RemoveRange(logistics);
        _dbContext.ParserCurrentProductRanks.RemoveRange(ranks);
        _dbContext.ParserCurrentProductReviewsSummaries.RemoveRange(reviewSummaries);
        _dbContext.ParserCurrentProductReviewEvidence.RemoveRange(reviewEvidence);
        _dbContext.ParserCurrentProductRows.RemoveRange(products);
        _dbContext.ParserProductRows.RemoveRange(productRows);
        return products.Count;
    }

    private async Task DeleteCreatedEntityAsync(ParserRunCurrentEntityEffect effect, CancellationToken cancellationToken)
    {
        var id = ParseEntityGuid(effect.EntityKey);
        if (!id.HasValue)
            return;

        switch (effect.EntityKind)
        {
            case ParserRunCurrentEntityKinds.Details:
                var details = await _dbContext.ParserCurrentProductDetails.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                if (details is not null) _dbContext.ParserCurrentProductDetails.Remove(details);
                break;
            case ParserRunCurrentEntityKinds.Logistics:
                var logistics = await _dbContext.ParserCurrentProductLogistics.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                if (logistics is not null) _dbContext.ParserCurrentProductLogistics.Remove(logistics);
                break;
            case ParserRunCurrentEntityKinds.Rank:
                var rank = await _dbContext.ParserCurrentProductRanks.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                if (rank is not null) _dbContext.ParserCurrentProductRanks.Remove(rank);
                break;
            case ParserRunCurrentEntityKinds.ReviewsSummary:
                var summary = await _dbContext.ParserCurrentProductReviewsSummaries.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                if (summary is not null) _dbContext.ParserCurrentProductReviewsSummaries.Remove(summary);
                break;
            case ParserRunCurrentEntityKinds.ReviewEvidence:
                var evidence = await _dbContext.ParserCurrentProductReviewEvidence.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                if (evidence is not null) _dbContext.ParserCurrentProductReviewEvidence.Remove(evidence);
                break;
        }
    }

    private async Task RestoreEntityAsync(ParserRunCurrentEntityEffect effect, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var id = ParseEntityGuid(effect.EntityKey);
        if (!id.HasValue)
            return;

        switch (effect.EntityKind)
        {
            case ParserRunCurrentEntityKinds.Product:
                var product = await _dbContext.ParserCurrentProductRows.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                var productSnapshot = Deserialize<ParserCurrentProductSnapshot>(effect.OldValueJson);
                if (product is not null && productSnapshot is not null)
                    product.Restore(productSnapshot, nowUtc);
                break;
            case ParserRunCurrentEntityKinds.Details:
                var details = await _dbContext.ParserCurrentProductDetails.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                var detailsSnapshot = Deserialize<ParserCurrentSimpleSnapshot>(effect.OldValueJson);
                if (details is not null && detailsSnapshot is not null)
                    details.Restore(detailsSnapshot);
                break;
            case ParserRunCurrentEntityKinds.Logistics:
                var logistics = await _dbContext.ParserCurrentProductLogistics.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                var logisticsSnapshot = Deserialize<ParserCurrentSimpleSnapshot>(effect.OldValueJson);
                if (logistics is not null && logisticsSnapshot is not null)
                    logistics.Restore(logisticsSnapshot);
                break;
            case ParserRunCurrentEntityKinds.Rank:
                var rank = await _dbContext.ParserCurrentProductRanks.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                var rankSnapshot = Deserialize<ParserCurrentSimpleSnapshot>(effect.OldValueJson);
                if (rank is not null && rankSnapshot is not null)
                    rank.Restore(rankSnapshot);
                break;
            case ParserRunCurrentEntityKinds.ReviewsSummary:
                var summary = await _dbContext.ParserCurrentProductReviewsSummaries.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                var summarySnapshot = Deserialize<ParserCurrentReviewsSummarySnapshot>(effect.OldValueJson);
                if (summary is not null && summarySnapshot is not null)
                    summary.Restore(summarySnapshot);
                break;
            case ParserRunCurrentEntityKinds.ReviewEvidence:
                var evidence = await _dbContext.ParserCurrentProductReviewEvidence.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                var evidenceSnapshot = Deserialize<ParserCurrentReviewEvidenceSnapshot>(effect.OldValueJson);
                if (evidence is not null && evidenceSnapshot is not null)
                    evidence.Restore(evidenceSnapshot);
                break;
        }
    }

    private async Task<ServiceResult> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.RoleId.HasValue)
            return ServiceResult.Unauthorized("Authentication is required.");

        var isAdmin = await _dbContext.Roles
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == _currentUser.RoleId.Value &&
                     x.Name == AdminRoleName,
                cancellationToken);

        return isAdmin
            ? ServiceResult.Success()
            : ServiceResult.Forbidden("Administrator role is required.");
    }

    private static ServiceResult<T> AccessError<T>(ServiceError error)
    {
        return error.Type switch
        {
            ServiceErrorType.Unauthorized => ServiceResult<T>.Unauthorized(error.Message),
            ServiceErrorType.Forbidden => ServiceResult<T>.Forbidden(error.Message),
            _ => ServiceResult<T>.Forbidden(error.Message)
        };
    }

    private static Guid? ParseEntityGuid(string value) =>
        Guid.TryParse(value, out var id) ? id : null;

    private static T? Deserialize<T>(string? value) =>
        string.IsNullOrWhiteSpace(value) ? default : JsonSerializer.Deserialize<T>(value, JsonOptions);
}
