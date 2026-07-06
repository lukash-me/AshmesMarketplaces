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
    JsonElement Payload,
    string? ParserCycleId = null,
    string? ExternalProxyRunId = null);

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
    string? ParserCycleId,
    string? CycleKind,
    string ProxyKey,
    string SourceCategory,
    string SourceSubcategory,
    int PlannedProductsCount,
    int DownloadedProductsCount,
    string? EgressIp = null,
    string? TokenRef = null,
    string? SessionStatus = null,
    string? Phase = null,
    int? PlannedRangesCount = null,
    int? CompletedRangesCount = null,
    double? RangeProgressPercent = null,
    int? RangeChecksCount = null,
    int? FinalRangesCount = null,
    int? EmptyRangesCount = null,
    int? SplitRangesCount = null);

public sealed record ParserProxyRunProgressRequest(
    string ParserInstanceId,
    int PlannedProductsCount,
    int DownloadedProductsCount,
    string? Phase = null,
    int? PlannedRangesCount = null,
    int? CompletedRangesCount = null,
    double? RangeProgressPercent = null,
    int? RangeChecksCount = null,
    int? FinalRangesCount = null,
    int? EmptyRangesCount = null,
    int? SplitRangesCount = null);

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
    string ParserCycleId,
    string CycleKind,
    string ProxyKey,
    string SourceCategory,
    string SourceSubcategory,
    string? EgressIp,
    string? TokenRef,
    string? SessionStatus,
    string Status,
    string Phase,
    int PlannedProductsCount,
    int DownloadedProductsCount,
    int PlannedRangesCount,
    int CompletedRangesCount,
    double RangeProgressPercent,
    int RangeChecksCount,
    int FinalRangesCount,
    int EmptyRangesCount,
    int SplitRangesCount,
    DateTime StartedAtUtc,
    DateTime LastHeartbeatAtUtc,
    DateTime? FinishedAtUtc,
    string? Error);

public sealed record ParserPriceSplitEnsureJobRequest(
    string ParserInstanceId,
    string ProxyKey,
    string SourceCategory,
    string SourceSubcategory,
    int MinPriceU,
    int MaxPriceU);

public sealed record ParserPriceSplitCurrentJobRequest(
    string ParserInstanceId,
    string ProxyKey,
    string SourceCategory,
    string SourceSubcategory);

public sealed record ParserPriceSplitJobResponse(
    Guid Id,
    string ParserInstanceId,
    string ProxyKey,
    string SourceCategory,
    string SourceSubcategory,
    string Status,
    int MinPriceU,
    int MaxPriceU,
    int TotalRangesCount,
    int CompletedRangesCount);

public sealed record ParserPriceSplitClaimRangesRequest(
    Guid JobId,
    string ParserInstanceId,
    string ProxyKey,
    int Limit);

public sealed record ParserPriceSplitRangeResponse(
    Guid Id,
    Guid JobId,
    Guid? ParentRangeId,
    int MinPriceU,
    int MaxPriceU,
    int? ExpectedTotal,
    int NextPage,
    int NextItemOffset,
    string Status,
    int AttemptsCount,
    DateTime? CooldownUntilUtc,
    string? Error);

public sealed record ParserPriceSplitClaimRangesResponse(IReadOnlyList<ParserPriceSplitRangeResponse> Ranges);

public sealed record ParserPriceSplitUpdateRangeRequest(
    string ParserInstanceId,
    string ProxyKey,
    string Status,
    int? ExpectedTotal,
    DateTime? CooldownUntilUtc,
    string? Error,
    int? NextPage,
    int? NextItemOffset);

public sealed record ParserPriceSplitChildRangeRequest(int MinPriceU, int MaxPriceU, int? ExpectedTotal);

public sealed record ParserPriceSplitSplitRangeRequest(
    string ParserInstanceId,
    string ProxyKey,
    IReadOnlyList<ParserPriceSplitChildRangeRequest> Children);
