using System.Text.Json;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserBatchProcessingService : IParserBatchProcessingService
{
    private const int MaxAttempts = 5;
    private const string FullBatchArtifactKind = "full_batch";

    private static readonly string[] ClaimableStatuses =
    [
        ParserBatchStatuses.Accepted,
        ParserBatchStatuses.Queued,
        ParserBatchStatuses.FailedRetryable
    ];

    private readonly ApplicationDbContext _dbContext;
    private readonly IParserBatchPayloadProcessor _payloadProcessor;
    private readonly ParserProductPresenceReconciliationService? _presenceReconciliationService;

    public ParserBatchProcessingService(
        ApplicationDbContext dbContext,
        IParserBatchPayloadProcessor payloadProcessor,
        ParserProductPresenceReconciliationService? presenceReconciliationService = null)
    {
        _dbContext = dbContext;
        _payloadProcessor = payloadProcessor;
        _presenceReconciliationService = presenceReconciliationService;
    }

    public async Task<ParserBatchProcessingResult> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var batch = await ClaimNextAsync(cancellationToken);
        if (batch is null)
            return ParserBatchProcessingResult.NoWork();

        try
        {
            var payload = await LoadPayloadAsync(batch.Id, cancellationToken);
            var summary = await _payloadProcessor.ProcessAsync(batch, payload, cancellationToken);
            await CompleteAsync(
                batch.Id,
                $"Processed {summary.RowsRead} rows, wrote {summary.RowsWritten}, skipped {summary.RowsSkipped}.",
                cancellationToken);

            return new ParserBatchProcessingResult(
                true,
                batch.Id,
                batch.ExternalBatchId,
                ParserBatchStatuses.Completed,
                null);
        }
        catch (ParserBatchFinalException exception)
        {
            await FailFinalAsync(batch.Id, exception.Message, cancellationToken);
            return new ParserBatchProcessingResult(
                true,
                batch.Id,
                batch.ExternalBatchId,
                ParserBatchStatuses.FailedFinal,
                exception.Message);
        }
        catch (JsonException exception)
        {
            await FailFinalAsync(batch.Id, $"Batch payload is not valid JSON: {exception.Message}", cancellationToken);
            return new ParserBatchProcessingResult(
                true,
                batch.Id,
                batch.ExternalBatchId,
                ParserBatchStatuses.FailedFinal,
                exception.Message);
        }
        catch (InvalidDataException exception)
        {
            await FailFinalAsync(batch.Id, exception.Message, cancellationToken);
            return new ParserBatchProcessingResult(
                true,
                batch.Id,
                batch.ExternalBatchId,
                ParserBatchStatuses.FailedFinal,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await FailFinalAsync(batch.Id, exception.Message, cancellationToken);
            return new ParserBatchProcessingResult(
                true,
                batch.Id,
                batch.ExternalBatchId,
                ParserBatchStatuses.FailedFinal,
                exception.Message);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            var status = batch.AttemptsCount >= MaxAttempts
                ? ParserBatchStatuses.FailedFinal
                : ParserBatchStatuses.FailedRetryable;
            if (status == ParserBatchStatuses.FailedFinal)
                await FailFinalAsync(batch.Id, $"Retry limit exceeded. Last error: {exception.Message}", cancellationToken);
            else
                await FailRetryableAsync(batch.Id, exception.Message, cancellationToken);

            return new ParserBatchProcessingResult(
                true,
                batch.Id,
                batch.ExternalBatchId,
                status,
                exception.Message);
        }
    }

    private async Task<ParserBatchSubmission?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var candidate = await _dbContext.ParserBatchSubmissions
                .AsNoTracking()
                .Where(x =>
                    ClaimableStatuses.Contains(x.Status) &&
                    (x.Status != ParserBatchStatuses.FailedRetryable || x.AttemptsCount < MaxAttempts))
                .OrderBy(x => x.AcceptedAtUtc)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (candidate is null)
                return null;

            var now = DateTime.UtcNow;
            var claimed = await TryClaimAsync(candidate, now, cancellationToken);
            if (!claimed)
                continue;

            _dbContext.ParserBatchSubmissionEvents.Add(
                new ParserBatchSubmissionEvent(candidate.Id, "claimed", ParserBatchStatuses.Processing, null, now));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return await _dbContext.ParserBatchSubmissions
                .AsNoTracking()
                .SingleAsync(x => x.Id == candidate.Id, cancellationToken);
        }
    }

    private async Task<bool> TryClaimAsync(
        ParserBatchSubmission candidate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsRelational())
        {
            var updated = await _dbContext.ParserBatchSubmissions
                .Where(x =>
                    x.Id == candidate.Id &&
                    x.Status == candidate.Status &&
                    (x.Status != ParserBatchStatuses.FailedRetryable || x.AttemptsCount < MaxAttempts))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, ParserBatchStatuses.Processing)
                        .SetProperty(x => x.AttemptsCount, x => x.AttemptsCount + 1)
                        .SetProperty(x => x.ProcessingStartedAtUtc, now)
                        .SetProperty(x => x.CompletedAtUtc, (DateTime?)null)
                        .SetProperty(x => x.Error, (string?)null)
                        .SetProperty(x => x.UpdatedAtUtc, now),
                    cancellationToken);
            return updated == 1;
        }

        var tracked = await _dbContext.ParserBatchSubmissions
            .FirstOrDefaultAsync(
                x =>
                    x.Id == candidate.Id &&
                    x.Status == candidate.Status &&
                    (x.Status != ParserBatchStatuses.FailedRetryable || x.AttemptsCount < MaxAttempts),
                cancellationToken);
        if (tracked is null)
            return false;

        tracked.MarkProcessing(now);
        return true;
    }

    private async Task<JsonElement> LoadPayloadAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var artifact = await _dbContext.ParserBatchArtifacts
            .Where(x => x.BatchSubmissionId == batchId && x.ArtifactKind == FullBatchArtifactKind)
            .SingleOrDefaultAsync(cancellationToken);

        if (artifact is null)
            throw new ParserBatchFinalException("Batch payload artifact was not found.");

        return artifact.PayloadJson.RootElement.Clone();
    }

    private async Task CompleteAsync(Guid batchId, string message, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var batch = await LoadTrackedBatchAsync(batchId, cancellationToken);
        batch.MarkCompleted(now);
        var parserCycleId = batch.ParserCycleId;
        _dbContext.ParserBatchSubmissionEvents.Add(
            new ParserBatchSubmissionEvent(batchId, "completed", batch.Status, message, now));
        await DeleteRawArtifactsAsync(batchId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (_presenceReconciliationService is not null)
            await _presenceReconciliationService.TryReconcileCycleAsync(parserCycleId, cancellationToken);
    }

    private async Task FailRetryableAsync(Guid batchId, string error, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var batch = await LoadTrackedBatchAsync(batchId, cancellationToken);
        batch.MarkFailedRetryable(error, now);
        _dbContext.ParserBatchSubmissionEvents.Add(
            new ParserBatchSubmissionEvent(batchId, "failed_retryable", batch.Status, error, now));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task FailFinalAsync(Guid batchId, string error, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var batch = await LoadTrackedBatchAsync(batchId, cancellationToken);
        batch.MarkFailedFinal(error, now);
        var parserCycleId = batch.ParserCycleId;
        _dbContext.ParserBatchSubmissionEvents.Add(
            new ParserBatchSubmissionEvent(batchId, "failed_final", batch.Status, error, now));
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (_presenceReconciliationService is not null)
            await _presenceReconciliationService.TryReconcileCycleAsync(parserCycleId, cancellationToken);
    }

    private async Task<ParserBatchSubmission> LoadTrackedBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ParserBatchSubmissions
            .SingleAsync(x => x.Id == batchId, cancellationToken);
    }

    private async Task DeleteRawArtifactsAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var artifacts = await _dbContext.ParserBatchArtifacts
            .Where(x => x.BatchSubmissionId == batchId)
            .ToListAsync(cancellationToken);

        _dbContext.ParserBatchArtifacts.RemoveRange(artifacts);
    }
}
