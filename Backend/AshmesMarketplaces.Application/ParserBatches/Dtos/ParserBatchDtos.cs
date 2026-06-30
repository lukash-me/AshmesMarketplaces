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

public sealed record ParserProxyRunStartRequest(
    string ParserInstanceId,
    string ExternalProxyRunId,
    string ProxyKey,
    string SourceCategory,
    string SourceSubcategory,
    int PlannedProductsCount,
    int DownloadedProductsCount);

public sealed record ParserProxyRunProgressRequest(
    string ParserInstanceId,
    int PlannedProductsCount,
    int DownloadedProductsCount);

public sealed record ParserProxyRunFinishRequest(
    string ParserInstanceId,
    string Status,
    int PlannedProductsCount,
    int DownloadedProductsCount,
    string? Error);

public sealed record ParserProxyRunResponse(
    Guid Id,
    string ParserInstanceId,
    string ExternalProxyRunId,
    string ProxyKey,
    string SourceCategory,
    string SourceSubcategory,
    string Status,
    int PlannedProductsCount,
    int DownloadedProductsCount,
    DateTime StartedAtUtc,
    DateTime LastHeartbeatAtUtc,
    DateTime? FinishedAtUtc,
    string? Error);
