namespace AshmesMarketplaces.Application.ParserBatches.Dtos;

public sealed record CreateParserLaunchRequest(
    string Mode,
    int? BatchLimit,
    string? ProxyKey);

public sealed record ParserLaunchRequestDto(
    Guid Id,
    string ParserInstanceId,
    string Mode,
    string? ProxyKey,
    int? BatchLimit,
    string Status,
    DateTime RequestedAtUtc);
