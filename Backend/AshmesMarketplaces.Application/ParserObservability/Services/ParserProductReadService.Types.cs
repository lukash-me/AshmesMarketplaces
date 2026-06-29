using System.Text.Json;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed partial class ParserProductReadService
{
    private sealed record DemoCardDetailCharacteristicsRow(
        string Subcategory,
        JsonDocument? Characteristics);

    private sealed record ParserRunScope(
        bool IncludeTestRuns,
        bool TestRunsOnly,
        string? TestLabel)
    {
        public static ParserRunScope Production { get; } = new(false, false, null);
        public bool IsDefaultProduction => !IncludeTestRuns && !TestRunsOnly && string.IsNullOrWhiteSpace(TestLabel);

        public static ParserRunScope From(bool includeTestRuns, bool testRunsOnly, string? testLabel)
        {
            return new ParserRunScope(
                IncludeTestRuns: includeTestRuns || testRunsOnly,
                TestRunsOnly: testRunsOnly,
                TestLabel: string.IsNullOrWhiteSpace(testLabel) ? null : testLabel.Trim());
        }

        public bool Matches(ParserRun run)
        {
            var isTest = IsTestRun(run);
            if (TestRunsOnly && !isTest)
                return false;

            if (!IncludeTestRuns && isTest)
                return false;

            if (!string.IsNullOrWhiteSpace(TestLabel)
                && !string.Equals(TestLabelOf(run), TestLabel, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public static bool IsTestRun(ParserRun run)
        {
            var value = RequestedScopeValue(run, "is_test_run");
            return value?.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.String => bool.TryParse(value.Value.GetString(), out var parsed) && parsed,
                _ => false
            };
        }

        public static string? TestLabelOf(ParserRun run)
        {
            var value = RequestedScopeValue(run, "test_label");
            return value?.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        }

        private static JsonElement? RequestedScopeValue(ParserRun run, string propertyName)
        {
            if (run.RequestedScope is null || run.RequestedScope.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            return run.RequestedScope.RootElement.TryGetProperty(propertyName, out var value)
                ? value
                : null;
        }
    }

    private sealed record ProductEvidenceLookup(
        IReadOnlyDictionary<Guid, ParserProductRankSummaryDto> Ranks,
        IReadOnlyDictionary<Guid, ParserProductPositionDto> Positions,
        IReadOnlyDictionary<Guid, ParserProductReviewEvidenceDto> Reviews,
        IReadOnlyDictionary<Guid, ParserProductLogisticsSummaryDto> LogisticsSummaries,
        IReadOnlyDictionary<Guid, ParserProductLogisticsDetailDto> LogisticsDetails,
        IReadOnlyDictionary<Guid, ParserProductDeliveryProfileDto> DeliveryProfiles)
    {
        public static ProductEvidenceLookup Empty { get; } = new(
            new Dictionary<Guid, ParserProductRankSummaryDto>(),
            new Dictionary<Guid, ParserProductPositionDto>(),
            new Dictionary<Guid, ParserProductReviewEvidenceDto>(),
            new Dictionary<Guid, ParserProductLogisticsSummaryDto>(),
            new Dictionary<Guid, ParserProductLogisticsDetailDto>(),
            new Dictionary<Guid, ParserProductDeliveryProfileDto>());

        public ParserProductRankSummaryDto? GetRank(Guid productId)
        {
            return Ranks.TryGetValue(productId, out var rank) ? rank : null;
        }

        public ParserProductPositionDto? GetPosition(Guid productId)
        {
            return Positions.TryGetValue(productId, out var position) ? position : null;
        }

        public ParserProductReviewEvidenceDto GetReviewEvidence(Guid productId)
        {
            return Reviews.TryGetValue(productId, out var evidence) ? evidence : EmptyReviewEvidence;
        }

        public ParserProductLogisticsSummaryDto? GetLogisticsSummary(Guid productId)
        {
            return LogisticsSummaries.TryGetValue(productId, out var logistics) ? logistics : null;
        }

        public ParserProductLogisticsDetailDto? GetLogisticsDetail(Guid productId)
        {
            return LogisticsDetails.TryGetValue(productId, out var logistics) ? logistics : null;
        }

        public ParserProductDeliveryProfileDto? GetDeliveryProfile(Guid productId)
        {
            return DeliveryProfiles.TryGetValue(productId, out var profile) ? profile : null;
        }
    }

    private sealed record ProductLogisticsEvidence(
        IReadOnlyDictionary<Guid, ParserProductLogisticsSummaryDto> Summaries,
        IReadOnlyDictionary<Guid, ParserProductLogisticsDetailDto> Details,
        IReadOnlyDictionary<Guid, ParserProductDeliveryProfileDto> DeliveryProfiles)
    {
        public static ProductLogisticsEvidence Empty { get; } = new(
            new Dictionary<Guid, ParserProductLogisticsSummaryDto>(),
            new Dictionary<Guid, ParserProductLogisticsDetailDto>(),
            new Dictionary<Guid, ParserProductDeliveryProfileDto>());
    }

    private sealed record SelectedLogisticsObservation(
        Guid ProductRowId,
        LogisticsSnapshotCandidate Snapshot,
        IReadOnlyList<WarehouseAvailabilityCandidate> WarehouseRows)
    {
        public int DistinctWarehouseCount { get; } = WarehouseRows
            .Select(x => x.WarehouseIdOnMp)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private sealed record LogisticsSnapshotCandidate(
        long SourceLineNumber,
        string ParserRunId,
        DateTime ObservedAtUtc,
        string SourceRegionDest,
        string? DeliveryProfileKey,
        string? DeliveryDestinationName,
        string? DeliveryProfileVersion,
        string? DeliveryDestinationCity,
        string? DeliveryDestinationLabel,
        string? DeliveryDestinationAddress,
        decimal? DeliveryDestinationLatitude,
        decimal? DeliveryDestinationLongitude,
        string WbProductId,
        int? TotalQuantityObserved,
        bool? QuantityIsCapped,
        int? QuantityCapObserved,
        string QuantitySemantics,
        string? ProductWhRaw,
        int? ProductTime1Raw,
        int? ProductTime2Raw,
        long? ProductDtypeRaw,
        int? ProductDistRaw,
        string? VisibleDeliveryStatus,
        string? VisibleDeliveryLabel,
        DateTime? VisibleDeliveryDate,
        string? VisibleDeliverySource,
        DateTime? VisibleDeliveryObservedAtUtc,
        JsonDocument? VisibleDeliveryRawPayload);

    private sealed record WarehouseAvailabilityCandidate(
        long SourceLineNumber,
        string ParserRunId,
        string SourceRegionDest,
        string? DeliveryProfileKey,
        string? DeliveryDestinationName,
        string? DeliveryProfileVersion,
        string? DeliveryDestinationCity,
        string? DeliveryDestinationLabel,
        string? DeliveryDestinationAddress,
        decimal? DeliveryDestinationLatitude,
        decimal? DeliveryDestinationLongitude,
        string WbProductId,
        string? WarehouseIdOnMp,
        string? OptionId,
        string? SizeName,
        string? SizeOrigName,
        int? QuantityObserved,
        bool? QuantityIsCapped,
        int? QuantityCapObserved,
        string QuantitySemantics,
        int? StockPriorityRaw,
        int? StockTime1Raw,
        int? StockTime2Raw,
        long? StockDtypeRaw,
        int? StockDistRaw,
        decimal? PriceBasic,
        decimal? PriceProduct,
        decimal? PriceLogisticsRaw,
        decimal? PriceReturnRaw);

    private readonly record struct LogisticsWarehouseKey(string WbProductId, string SourceRegionDest);

    private sealed record RankCandidate(
        bool IsRootKey,
        string Key,
        int AbsolutePosition,
        int Page,
        int PositionOnPage,
        string Query,
        string? SourceCategory,
        string? SourceSubcategory,
        string? SourceRegionDest,
        string? Sort,
        DateTime ObservedAtUtc,
        string ParserRunId,
        string RankContextId);

    private readonly record struct RankMatchKey(bool IsRootKey, string Key);

    private sealed record PositionCoverageCandidate(
        string? SourceCategory,
        string? SourceSubcategory,
        string? SourceRegionDest,
        string? WbRootId,
        string WbProductId,
        string Query,
        int AbsolutePosition,
        DateTime ObservedAtUtc);

    private sealed record PositionCoverage(
        int ObservedRangeLimit,
        string? Query,
        DateTime ObservedAtUtc);

    private readonly record struct PositionCoverageKey(
        string? SourceCategory,
        string? SourceSubcategory,
        string? SourceRegionDest);

    private sealed record RootFetchAggregate(
        string Key,
        int RootFetchCount,
        string? LatestRunId,
        DateTime LatestAtUtc,
        bool IsFullHistoryUnknown,
        bool HasCappedRootPayload);

    private sealed record ReviewRowAggregate(
        bool IsRootKey,
        string Key,
        string? ProductRunId,
        int ParsedReviewCount,
        int RootFetchCount,
        string? LatestRunId,
        DateTime LatestAtUtc,
        string? AttributionMode,
        bool IsFullHistoryUnknown,
        bool HasCappedRootPayload);

    private sealed record ReplyRowAggregate(
        bool IsRootKey,
        string Key,
        string? ProductRunId,
        int ParsedReplyCount,
        int RootFetchCount,
        string? LatestRunId,
        DateTime LatestAtUtc,
        string? AttributionMode,
        bool IsFullHistoryUnknown,
        bool HasCappedRootPayload);

    private readonly record struct ReviewEvidenceKey(bool IsRootKey, string Key, string? ProductRunId);

    private sealed class ReviewEvidenceBucket
    {
        public int RootFetchCount { get; set; }
        public int ParsedReviewCount { get; set; }
        public int ParsedReplyCount { get; set; }
        public string? LatestReviewRunId { get; private set; }
        public DateTime? LatestAtUtc { get; private set; }
        public string? AttributionMode { get; set; }
        public bool IsFullHistoryUnknown { get; set; }
        public bool HasCappedRootPayload { get; set; }

        public void SetLatest(DateTime? latestAtUtc, string? latestRunId)
        {
            if (!latestAtUtc.HasValue || string.IsNullOrWhiteSpace(latestRunId))
                return;

            if (!LatestAtUtc.HasValue || latestAtUtc.Value > LatestAtUtc.Value)
            {
                LatestAtUtc = latestAtUtc.Value;
                LatestReviewRunId = latestRunId;
            }
        }
    }
}
