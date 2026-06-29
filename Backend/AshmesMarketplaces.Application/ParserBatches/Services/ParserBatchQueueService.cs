using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserBatchQueueService : IParserBatchQueueService
{
    private const string FullBatchArtifactKind = "full_batch";
    private readonly ApplicationDbContext _dbContext;

    public ParserBatchQueueService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<ParserBatchSubmitResponse>> SubmitAsync(
        ParserBatchSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return ServiceResult<ParserBatchSubmitResponse>.BadRequest(validationError);

        var payloadJson = request.Payload.GetRawText();
        var calculatedHash = CalculateContentHash(payloadJson);
        if (!string.IsNullOrWhiteSpace(request.ContentHash) &&
            !string.Equals(request.ContentHash.Trim(), calculatedHash, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<ParserBatchSubmitResponse>.BadRequest("Content hash does not match batch payload.");
        }

        var parserInstanceId = request.ParserInstanceId.Trim();
        var externalBatchId = request.ExternalBatchId.Trim();
        var existing = await _dbContext.ParserBatchSubmissions
            .FirstOrDefaultAsync(
                x => x.ParserInstanceId == parserInstanceId && x.ExternalBatchId == externalBatchId,
                cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.ContentHash, calculatedHash, StringComparison.OrdinalIgnoreCase))
                return ServiceResult<ParserBatchSubmitResponse>.Conflict("Parser batch already exists with another content hash.");

            return ServiceResult<ParserBatchSubmitResponse>.Success(MapSubmit(existing));
        }

        var now = DateTime.UtcNow;
        await UpsertParserInstanceAsync(parserInstanceId, now, cancellationToken);

        var batch = new ParserBatchSubmission(
            parserInstanceId,
            externalBatchId,
            request.SourceCategory,
            request.SourceSubcategory,
            request.ProxyKey,
            request.BatchKind,
            calculatedHash,
            now);

        var payloadDocument = JsonDocument.Parse(payloadJson);
        var artifact = new ParserBatchArtifact(batch.Id, FullBatchArtifactKind, payloadDocument, now);
        var acceptedEvent = new ParserBatchSubmissionEvent(batch.Id, "accepted", batch.Status, null, now);

        _dbContext.ParserBatchSubmissions.Add(batch);
        _dbContext.ParserBatchArtifacts.Add(artifact);
        _dbContext.ParserBatchSubmissionEvents.Add(acceptedEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ParserBatchSubmitResponse>.Success(MapSubmit(batch));
    }

    public async Task<ServiceResult<ParserBatchStatusResponse>> GetStatusAsync(
        string externalBatchId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalBatchId))
            return ServiceResult<ParserBatchStatusResponse>.BadRequest("External batch id is required.");

        var batch = await _dbContext.ParserBatchSubmissions
            .AsNoTracking()
            .Where(x => x.ExternalBatchId == externalBatchId.Trim())
            .OrderByDescending(x => x.AcceptedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return batch is null
            ? ServiceResult<ParserBatchStatusResponse>.NotFound("Parser batch was not found.")
            : ServiceResult<ParserBatchStatusResponse>.Success(MapStatus(batch));
    }

    public async Task<ServiceResult<ParserPendingAckResponse>> GetPendingAcksAsync(
        string parserInstanceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parserInstanceId))
            return ServiceResult<ParserPendingAckResponse>.BadRequest("Parser instance id is required.");

        var normalizedParserInstanceId = parserInstanceId.Trim();
        var batches = await _dbContext.ParserBatchSubmissions
            .AsNoTracking()
            .Where(x =>
                x.ParserInstanceId == normalizedParserInstanceId &&
                x.AcknowledgedAtUtc == null &&
                (x.Status == ParserBatchStatuses.Completed ||
                 x.Status == ParserBatchStatuses.FailedRetryable ||
                 x.Status == ParserBatchStatuses.FailedFinal))
            .OrderBy(x => x.CompletedAtUtc ?? x.UpdatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(200)
            .ToListAsync(cancellationToken);

        return ServiceResult<ParserPendingAckResponse>.Success(
            new ParserPendingAckResponse(batches.Select(MapStatus).ToList()));
    }

    private async Task UpsertParserInstanceAsync(string parserInstanceId, DateTime now, CancellationToken cancellationToken)
    {
        var instance = await _dbContext.ParserInstances
            .FirstOrDefaultAsync(x => x.ParserInstanceId == parserInstanceId, cancellationToken);

        if (instance is null)
        {
            _dbContext.ParserInstances.Add(new ParserInstance(parserInstanceId, parserInstanceId, now));
            return;
        }

        instance.Touch(now);
    }

    private static string? Validate(ParserBatchSubmitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ParserInstanceId))
            return "Parser instance id is required.";
        if (string.IsNullOrWhiteSpace(request.ExternalBatchId))
            return "External batch id is required.";
        if (string.IsNullOrWhiteSpace(request.SourceCategory))
            return "Source category is required.";
        if (string.IsNullOrWhiteSpace(request.SourceSubcategory))
            return "Source subcategory is required.";
        if (string.IsNullOrWhiteSpace(request.BatchKind))
            return "Batch kind is required.";
        if (request.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return "Batch payload is required.";

        return null;
    }

    private static string CalculateContentHash(string payloadJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static ParserBatchSubmitResponse MapSubmit(ParserBatchSubmission batch) =>
        new(batch.Id, batch.ParserInstanceId, batch.ExternalBatchId, batch.ContentHash, batch.Status, batch.AcceptedAtUtc);

    private static ParserBatchStatusResponse MapStatus(ParserBatchSubmission batch) =>
        new(
            batch.Id,
            batch.ParserInstanceId,
            batch.ExternalBatchId,
            batch.SourceCategory,
            batch.SourceSubcategory,
            batch.ProxyKey,
            batch.BatchKind,
            batch.ContentHash,
            batch.Status,
            batch.AttemptsCount,
            batch.AcceptedAtUtc,
            batch.ProcessingStartedAtUtc,
            batch.CompletedAtUtc,
            batch.Error);
}
