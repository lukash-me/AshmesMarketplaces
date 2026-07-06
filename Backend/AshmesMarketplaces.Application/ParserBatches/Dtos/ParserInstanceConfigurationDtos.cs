namespace AshmesMarketplaces.Application.ParserBatches.Dtos;

public sealed record ParserInstanceConfiguredProxyDto(
    Guid ProxyId,
    string ProxyKey,
    string Ip,
    string? SourceSubcategory,
    bool AssignmentEnabled);

public sealed record ParserInstanceConfigurationDto(
    Guid Id,
    string ParserInstanceId,
    string DisplayName,
    string HostKind,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ParserInstanceConfiguredProxyDto> Proxies);

public sealed record ParserProxyAssignedInstanceDto(
    Guid InstanceConfigurationId,
    string ParserInstanceId,
    string DisplayName);

public sealed record CreateParserInstanceConfigurationRequest(
    string? DisplayName,
    IReadOnlyList<Guid>? ProxyIds);

public sealed record UpdateParserInstanceConfigurationRequest(
    string? DisplayName,
    IReadOnlyList<Guid>? ProxyIds,
    string? Status);
