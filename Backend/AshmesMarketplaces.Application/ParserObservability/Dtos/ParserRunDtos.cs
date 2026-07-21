using System.Text.Json;

namespace AshmesMarketplaces.Application.ParserObservability.Dtos;

public sealed record ParserRunSummaryDto(
    Guid Id,
    string ParserRunId,
    string Kind,
    string Marketplace,
    string ManifestStatus,
    string? ParserVersion,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    DateTime DateRegisteredUtc,
    JsonElement? RequestedScope,
    JsonElement? Counters);

public sealed class ParserRunListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Kind { get; init; }
    public string? ManifestStatus { get; init; }
    public string? ParserRunId { get; init; }
}
