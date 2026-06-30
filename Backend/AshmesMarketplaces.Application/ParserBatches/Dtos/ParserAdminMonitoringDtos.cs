namespace AshmesMarketplaces.Application.ParserBatches.Dtos;

public sealed record ParserAdminProxyRunDto(
    Guid Id,
    string ExternalProxyRunId,
    string ProxyKey,
    string SourceCategory,
    string SourceSubcategory,
    string Status,
    int PlannedProductsCount,
    int DownloadedProductsCount,
    double ProgressPercent,
    DateTime StartedAtUtc,
    DateTime LastHeartbeatAtUtc,
    DateTime? FinishedAtUtc,
    int RuntimeMinutes,
    string? Error);

public sealed record ParserAdminInstanceDto(
    Guid Id,
    string ParserInstanceId,
    string? DisplayName,
    int RunningProxiesCount,
    int TotalProxiesCount,
    int PlannedProductsCount,
    int DownloadedProductsCount,
    double ProgressPercent,
    int RuntimeMinutes,
    DateTime LastHeartbeatUtc,
    IReadOnlyList<ParserAdminProxyRunDto> Proxies);

public sealed record ParserAdminProxyRunJournalDto(
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
    DateTime? FinishedAtUtc,
    int RuntimeMinutes,
    string? Error);

public sealed record ParserAdminBatchListItemDto(
    Guid Id,
    string ParserInstanceId,
    string ExternalBatchId,
    string SourceCategory,
    string SourceSubcategory,
    string? ProxyKey,
    string BatchKind,
    string Status,
    int AttemptsCount,
    DateTime AcceptedAtUtc,
    DateTime? ProcessingStartedAtUtc,
    DateTime? CompletedAtUtc,
    string? Error);

public sealed record ParserAdminBatchEventDto(
    Guid Id,
    string EventType,
    string Status,
    string? Message,
    DateTime CreatedAtUtc);

public sealed record ParserAdminBatchArtifactDto(
    Guid Id,
    string ArtifactKind,
    DateTime CreatedAtUtc);

public sealed record ParserAdminBatchDetailDto(
    ParserAdminBatchListItemDto Batch,
    IReadOnlyList<ParserAdminBatchEventDto> Events,
    IReadOnlyList<ParserAdminBatchArtifactDto> Artifacts);

public sealed record ParserAdminNicheDto(
    string SourceCategory,
    string SourceSubcategory,
    string? AssignedParserInstanceId,
    string? LastProxyKey,
    bool IsEnabled,
    DateTime? LastSuccessfulBatchAtUtc,
    double? LagHours,
    int FailedBatchesCount);

public sealed record ParserAdminErrorDto(
    Guid? BatchId,
    string? ParserInstanceId,
    string? ExternalBatchId,
    string? SourceCategory,
    string? SourceSubcategory,
    string? ProxyKey,
    string Status,
    string Error,
    DateTime CreatedAtUtc);

public sealed record ParserAdminRetryResponse(
    Guid Id,
    string ExternalBatchId,
    string Status,
    DateTime QueuedAtUtc);
