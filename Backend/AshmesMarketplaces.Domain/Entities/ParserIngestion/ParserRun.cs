using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserRun : IDisposable
{
    private ParserRun() { }

    public ParserRun(
        string parserRunId,
        string marketplace,
        string kind,
        string manifestStatus,
        int schemaVersion,
        string? parserVersion,
        DateTime startedAtUtc,
        DateTime? finishedAtUtc,
        JsonDocument? requestedScope,
        JsonDocument? counters,
        DateTime dateRegisteredUtc)
    {
        if (string.IsNullOrWhiteSpace(parserRunId))
            throw new ArgumentException("Parser run id is required.", nameof(parserRunId));

        if (string.IsNullOrWhiteSpace(marketplace))
            throw new ArgumentException("Marketplace is required.", nameof(marketplace));

        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("Parser run kind is required.", nameof(kind));

        if (string.IsNullOrWhiteSpace(manifestStatus))
            throw new ArgumentException("Manifest status is required.", nameof(manifestStatus));

        DateTimeUtc.EnsureUtc(startedAtUtc, nameof(startedAtUtc));
        DateTimeUtc.EnsureUtc(finishedAtUtc, nameof(finishedAtUtc));
        DateTimeUtc.EnsureUtc(dateRegisteredUtc, nameof(dateRegisteredUtc));

        Id = Guid.NewGuid();
        ParserRunId = parserRunId;
        Marketplace = marketplace;
        Kind = kind;
        ManifestStatus = manifestStatus;
        SchemaVersion = schemaVersion;
        ParserVersion = parserVersion;
        StartedAtUtc = startedAtUtc;
        FinishedAtUtc = finishedAtUtc;
        RequestedScope = requestedScope;
        Counters = counters;
        DateRegisteredUtc = dateRegisteredUtc;
    }

    public Guid Id { get; private set; }
    public string ParserRunId { get; private set; } = string.Empty;
    public string Marketplace { get; private set; } = string.Empty;
    public string Kind { get; private set; } = string.Empty;
    public string ManifestStatus { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string? ParserVersion { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public JsonDocument? RequestedScope { get; private set; }
    public JsonDocument? Counters { get; private set; }
    public DateTime DateRegisteredUtc { get; private set; }

    public void Dispose()
    {
        RequestedScope?.Dispose();
        Counters?.Dispose();
    }
}
