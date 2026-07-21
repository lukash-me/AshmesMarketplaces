namespace AshmesMarketplaces.Application.ParserBatches.Dtos;

public sealed record ParserProxyAssignmentDto(
    Guid Id,
    long WbCategoryId,
    string SourceCategory,
    string SourceSubcategory,
    string SourcePath,
    string SearchQuery,
    string ParserSearchText,
    string ScopeAcceptanceMode,
    IReadOnlyList<long> AllowedSubjectIds,
    bool Enabled);

public sealed record ParserProxyDto(
    Guid Id,
    string Key,
    string Ip,
    int HttpPort,
    int SocksPort,
    string Login,
    bool HasPassword,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    ParserProxyAssignmentDto? Assignment,
    ParserProxyAssignedInstanceDto? AssignedInstance);

public sealed record CreateParserProxyRequest(
    string? Ip,
    int HttpPort,
    int SocksPort,
    string? Login,
    string? Password,
    long? WbCategoryId);

public sealed record UpdateParserProxyRequest(
    string? Ip,
    int HttpPort,
    int SocksPort,
    string? Login,
    string? Password,
    long? WbCategoryId,
    bool? AssignmentEnabled,
    string? Status);

public sealed record ParserRuntimeProxyAssignmentsDto(
    ParserRuntimeProxyDefinitionDto DefaultProxy,
    IReadOnlyList<ParserRuntimeProxyDefinitionDto> Proxies,
    IReadOnlyList<ParserRuntimeNicheAssignmentDto> Niches);

public sealed record ParserRuntimeProxyDefinitionDto(
    string Key,
    string Type,
    string? BaseUrl,
    string? Socks5Url,
    ParserRuntimeProxyCredentialsDto? Credentials,
    string? Healthcheck = null,
    int? RateLimitPerMinute = null,
    int CooldownSeconds = 300);

public sealed record ParserRuntimeProxyCredentialsDto(
    string Login,
    string Password);

public sealed record ParserRuntimeNicheAssignmentDto(
    long WbCategoryId,
    string SourceCategory,
    string SourceSubcategory,
    string SourcePath,
    string SearchQuery,
    string ParserSearchText,
    string ScopeAcceptanceMode,
    IReadOnlyList<long> AllowedSubjectIds,
    string ProxyKey,
    bool Enabled);

public sealed record WbCategoryScopeSubjectMappingDto(
    Guid Id,
    long WbMenuId,
    string MenuToken,
    string SourcePath,
    long SubjectId,
    string? SubjectName,
    string Status,
    string MappingSource,
    DateTime ObservedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateWbCategoryScopeSubjectMappingRequest(string? Status);

public sealed record ParserRuntimeReviewSyncStateDto(
    string WbProductId,
    int? MarketplaceFeedbackCount,
    int FetchedReviewsCount,
    string CoverageStatus,
    DateTime? LatestReviewDateUtc,
    IReadOnlyList<string> KnownReviewIds,
    IReadOnlyList<string> UnansweredReviewIds);
