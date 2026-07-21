using System.Text.Json;
using AshmesMarketplaces.Application.ParserIngestion.Dtos;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;

namespace AshmesMarketplaces.Application.ParserIngestion.Services;

public sealed partial class ParserIngestionService
{
    private sealed record ReviewIdentity(string Marketplace, string WbProductId, string ReviewIdOnMp);

    private sealed record ReviewReplyIdentity(
        string Marketplace,
        string WbProductId,
        string ReviewIdOnMp,
        string ReplyKey);

    private sealed record ManifestInfo(
        string Kind,
        string ParserRunId,
        string Marketplace,
        string Status,
        int SchemaVersion,
        string? ParserVersion,
        DateTime StartedAtUtc,
        DateTime? FinishedAtUtc,
        JsonElement? RequestedScope,
        JsonElement? Counters)
    {
        public bool IsPartialSnapshot => !string.Equals(Status, "succeeded", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record BatchManifestInfo(
        string BatchId,
        int Size,
        string ProductRunDirectory,
        string ProductParserRunId,
        IReadOnlyList<string> LogisticsRunDirectories,
        IReadOnlyList<string> ReviewRunDirectories,
        IReadOnlyList<string> ProductDetailsRunDirectories);

    private sealed record RegisteredRun(ParserRun Run, IReadOnlyDictionary<string, ParserFile> Files);

    private sealed record RootFetchRow(
        Guid Id,
        string SourceWbRootId,
        bool IsPartialSnapshot,
        bool IsCappedRootPayload,
        bool IsFullHistoryUnknown);

    private sealed record RootFetchSnapshot(
        Guid? Id,
        bool IsPartialSnapshot,
        bool IsCappedRootPayload,
        bool IsFullHistoryUnknown);

    private sealed class JsonLine : IDisposable
    {
        public JsonLine(long lineNumber, string rawLine, JsonDocument payload, string? parseError)
        {
            LineNumber = lineNumber;
            RawLine = rawLine;
            Payload = payload;
            ParseError = parseError;
        }

        public long LineNumber { get; }
        public string RawLine { get; }
        public JsonDocument Payload { get; }
        public string? ParseError { get; }

        public void ThrowIfInvalid()
        {
            if (!string.IsNullOrWhiteSpace(ParseError))
                throw new InvalidDataException(ParseError);
        }

        public void Dispose()
        {
            Payload.Dispose();
        }
    }

    private sealed class ImportSummary
    {
        private readonly Dictionary<string, long> _details = new(StringComparer.Ordinal);

        public ImportSummary(string mode, string parserRunId, bool isDryRun)
        {
            Mode = mode;
            ParserRunId = parserRunId;
            IsDryRun = isDryRun;
        }

        public string Mode { get; }
        public string ParserRunId { get; }
        public bool IsDryRun { get; }
        public long RowsRead { get; set; }
        public long RowsWritten { get; set; }
        public long RowsSkipped { get; set; }
        public long Errors { get; set; }

        public void Increment(string name, long count = 1)
        {
            _details[name] = _details.GetValueOrDefault(name) + count;
        }

        public void Absorb(ParserIngestionResult result)
        {
            RowsRead += result.RowsRead;
            RowsWritten += result.RowsWritten;
            RowsSkipped += result.RowsSkipped;
            Errors += result.ErrorCount;
            Increment($"{result.Mode}_runs");

            foreach (var (name, count) in result.Details)
                Increment($"{result.Mode}_{name}", count);
        }

        public ParserIngestionResult ToResult()
        {
            return new ParserIngestionResult(
                Mode,
                ParserRunId,
                IsDryRun,
                RowsRead,
                RowsWritten,
                RowsSkipped,
                Errors,
                _details);
        }
    }
}
