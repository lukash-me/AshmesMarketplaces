using System.Text.Json;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserLogMonitoringOptions
{
    public string? LogRoot { get; set; }
    public int StaleAfterSeconds { get; set; } = 210;
}

public sealed record ParserLogSnapshot(
    string ParserCycleId,
    string ProxyKey,
    string Phase,
    int PlannedProductsCount,
    int DownloadedProductsCount,
    int RangeChecksCount,
    int FinalRangesCount,
    int EmptyRangesCount,
    int SplitRangesCount,
    DateTime? FirstLogAtUtc,
    DateTime? LastLogAtUtc,
    double ProductsPerSecond,
    double RangesPerSecond);

public interface IParserLogReader
{
    ParserLogMonitoringOptions Options { get; }

    ParserLogSnapshot? ReadProxyRun(string parserCycleId, string proxyKey);
}

public sealed class ParserLogReader : IParserLogReader
{
    private const string EventMarker = "PARSER_EVENT ";

    public ParserLogReader(ParserLogMonitoringOptions options)
    {
        Options = options;
    }

    public ParserLogMonitoringOptions Options { get; }

    public ParserLogSnapshot? ReadProxyRun(string parserCycleId, string proxyKey)
    {
        if (string.IsNullOrWhiteSpace(Options.LogRoot) ||
            string.IsNullOrWhiteSpace(parserCycleId) ||
            string.IsNullOrWhiteSpace(proxyKey))
            return null;

        var proxyLogDir = Path.Combine(Options.LogRoot, parserCycleId, proxyKey);
        if (!Directory.Exists(proxyLogDir))
            return null;

        var state = new SnapshotState(parserCycleId, proxyKey);
        foreach (var path in new[] { "runner.log", Path.Combine(proxyKey, "stdout.log"), Path.Combine(proxyKey, "stderr.log") })
        {
            var fullPath = path == "runner.log"
                ? Path.Combine(Options.LogRoot, parserCycleId, path)
                : Path.Combine(Options.LogRoot, parserCycleId, path);
            ReadEvents(fullPath, proxyKey, state);
        }

        return state.HasEvents ? state.ToSnapshot() : null;
    }

    private static void ReadEvents(string path, string proxyKey, SnapshotState state)
    {
        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadLines(path))
        {
            var markerIndex = line.IndexOf(EventMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
                continue;

            var json = line[(markerIndex + EventMarker.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var eventProxy = GetString(root, "proxyKey");
                if (!string.IsNullOrWhiteSpace(eventProxy) &&
                    !string.Equals(eventProxy, proxyKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                state.Apply(root);
            }
            catch (JsonException)
            {
                // Log monitoring is read-only diagnostic code. Broken individual lines must not
                // stop monitoring of later structured events in the same file.
            }
        }
    }

    private static string? GetString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private sealed class SnapshotState
    {
        private readonly string _parserCycleId;
        private readonly string _proxyKey;

        public SnapshotState(string parserCycleId, string proxyKey)
        {
            _parserCycleId = parserCycleId;
            _proxyKey = proxyKey;
        }

        public bool HasEvents { get; private set; }
        public string Phase { get; private set; } = "download";
        public int PlannedProductsCount { get; private set; }
        public int DownloadedProductsCount { get; private set; }
        public int RangeChecksCount { get; private set; }
        public int FinalRangesCount { get; private set; }
        public int EmptyRangesCount { get; private set; }
        public int SplitRangesCount { get; private set; }
        public DateTime? FirstLogAtUtc { get; private set; }
        public DateTime? LastLogAtUtc { get; private set; }

        public void Apply(JsonElement root)
        {
            HasEvents = true;
            var timestamp = GetDateTime(root, "timestampUtc");
            if (timestamp.HasValue)
            {
                FirstLogAtUtc = !FirstLogAtUtc.HasValue || timestamp.Value < FirstLogAtUtc.Value
                    ? timestamp.Value
                    : FirstLogAtUtc;
                LastLogAtUtc = !LastLogAtUtc.HasValue || timestamp.Value > LastLogAtUtc.Value
                    ? timestamp.Value
                    : LastLogAtUtc;
            }

            var phase = GetString(root, "phase");
            if (!string.IsNullOrWhiteSpace(phase))
                Phase = phase;
            else
                Phase = InferPhase(GetString(root, "event"), Phase);

            PlannedProductsCount = MaxValue(root, "plannedProductsCount", PlannedProductsCount);
            DownloadedProductsCount = MaxValue(root, "downloadedProductsCount", DownloadedProductsCount);
            RangeChecksCount = MaxValue(root, "rangeChecksCount", RangeChecksCount);
            FinalRangesCount = MaxValue(root, "finalRangesCount", FinalRangesCount);
            EmptyRangesCount = MaxValue(root, "emptyRangesCount", EmptyRangesCount);
            SplitRangesCount = MaxValue(root, "splitRangesCount", SplitRangesCount);
        }

        public ParserLogSnapshot ToSnapshot()
        {
            var elapsedSeconds = 0.0;
            if (FirstLogAtUtc.HasValue && LastLogAtUtc.HasValue && LastLogAtUtc.Value > FirstLogAtUtc.Value)
                elapsedSeconds = (LastLogAtUtc.Value - FirstLogAtUtc.Value).TotalSeconds;
            elapsedSeconds = Math.Max(1, elapsedSeconds);

            return new ParserLogSnapshot(
                _parserCycleId,
                _proxyKey,
                Phase,
                PlannedProductsCount,
                DownloadedProductsCount,
                RangeChecksCount,
                FinalRangesCount,
                EmptyRangesCount,
                SplitRangesCount,
                FirstLogAtUtc,
                LastLogAtUtc,
                Math.Round(DownloadedProductsCount / elapsedSeconds, 2),
                Math.Round(RangeChecksCount / elapsedSeconds, 2));
        }

        private static int MaxValue(JsonElement root, string name, int current)
        {
            if (!root.TryGetProperty(name, out var value))
                return current;

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var intValue) => Math.Max(current, intValue),
                _ => current
            };
        }

        private static DateTime? GetDateTime(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
                return null;

            return DateTime.TryParse(value.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed)
                ? DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc)
                : null;
        }

        private static string InferPhase(string? eventName, string current)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return current;
            if (eventName.StartsWith("stream_", StringComparison.OrdinalIgnoreCase))
                return "download";
            if (eventName.StartsWith("range_", StringComparison.OrdinalIgnoreCase))
                return "ranges";
            return current;
        }
    }
}
