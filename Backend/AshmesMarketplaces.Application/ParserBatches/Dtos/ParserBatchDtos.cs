using System.Text.Json;

namespace AshmesMarketplaces.Application.ParserBatches.Dtos;

public sealed record ParserBatchSubmitRequest(
    string ParserInstanceId,
    string ExternalBatchId,
    string SourceCategory,
    string SourceSubcategory,
    string? ProxyKey,
    string BatchKind,
    string? ContentHash,
    JsonElement Payload);

public sealed record ParserBatchSubmitResponse(
    Guid Id,
    string ParserInstanceId,
    string ExternalBatchId,
    string ContentHash,
    string Status,
    DateTime AcceptedAtUtc);

public sealed record ParserBatchStatusResponse(
    Guid Id,
    string ParserInstanceId,
    string ExternalBatchId,
    string SourceCategory,
    string SourceSubcategory,
    string? ProxyKey,
    string BatchKind,
    string ContentHash,
    string Status,
    int AttemptsCount,
    DateTime AcceptedAtUtc,
    DateTime? ProcessingStartedAtUtc,
    DateTime? CompletedAtUtc,
    string? Error);

public sealed record ParserPendingAckResponse(IReadOnlyList<ParserBatchStatusResponse> Items);
