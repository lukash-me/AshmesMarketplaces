using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserIngestion.Services;

public sealed record ParserReviewCoverageSnapshot(
    string WbProductId,
    string? WbRootId,
    int? MarketplaceFeedbackCount,
    int FetchedReviewsCount,
    string CoverageStatus,
    string CoverageSource,
    string? LastCoverageError,
    DateTime ObservedAtUtc);

public sealed class ParserProductCdcService
{
    private const string PositionStateObserved = "observed";
    private const string PositionStateBeyondObservedRange = "beyondObservedRange";
    private const string PositionStateUnknown = "unknown";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ApplicationDbContext _dbContext;

    public ParserProductCdcService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ApplyProductRowsAsync(
        IReadOnlyCollection<ParserProductRow> rows,
        string batchId,
        CancellationToken cancellationToken)
    {
        await ApplyProductRowsAsync(rows, batchId, null, cancellationToken);
    }

    public async Task ApplyProductRowsAsync(
        IReadOnlyCollection<ParserProductRow> rows,
        string batchId,
        ParserCdcApplyContext? context,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;

        var latestRows = rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(row => row.ParsedAtUtc).ThenByDescending(row => row.Id).First())
            .ToList();
        var ids = latestRows.Select(x => x.WbProductId).ToList();
        var currentRows = await _dbContext.ParserCurrentProductRows
            .Where(x => ids.Contains(x.WbProductId))
            .ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);
        var serverReceivedAtUtc = DateTime.UtcNow;
        var touchedCurrentRows = new List<ParserCurrentProductRow>(latestRows.Count);

        foreach (var row in latestRows)
        {
            var groups = ProductGroups(row);
            var hashes = ProductHashes(groups);
            if (!currentRows.TryGetValue(row.WbProductId, out var current))
            {
                var created = new ParserCurrentProductRow(row, hashes, serverReceivedAtUtc);
                MarkProductSeen(created, row, context, serverReceivedAtUtc);
                _dbContext.ParserCurrentProductRows.Add(created);
                touchedCurrentRows.Add(created);
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "identity", "created", null, hashes.IdentityHash, null, groups["identity"], serverReceivedAtUtc);
                RecordProductEffect(context, row.WbProductId, created.Id, ParserRunProductEffectTypes.Created, serverReceivedAtUtc);
                RecordCurrentEntityEffect(
                    context,
                    row.WbProductId,
                    ParserRunCurrentEntityKinds.Product,
                    created.Id.ToString("D"),
                    ParserRunCurrentEntityEffectTypes.Created,
                    null,
                    ParserRunCurrentEntitySnapshots.FromProduct(created),
                    serverReceivedAtUtc);
                continue;
            }

            touchedCurrentRows.Add(current);
            var oldPresenceStatus = current.MarketplacePresenceStatus;
            var oldGroups = ProductGroups(current);
            var oldSnapshot = ParserRunCurrentEntitySnapshots.FromProduct(current);
            var hasChange =
                !string.Equals(current.IdentityHash, hashes.IdentityHash, StringComparison.Ordinal) ||
                !string.Equals(current.PriceHash, hashes.PriceHash, StringComparison.Ordinal) ||
                !string.Equals(current.StockHash, hashes.StockHash, StringComparison.Ordinal) ||
                !string.Equals(current.RatingHash, hashes.RatingHash, StringComparison.Ordinal) ||
                !string.Equals(current.ReviewsHash, hashes.ReviewsHash, StringComparison.Ordinal) ||
                !string.Equals(current.MediaHash, hashes.MediaHash, StringComparison.Ordinal) ||
                !string.Equals(current.SellerBrandHash, hashes.SellerBrandHash, StringComparison.Ordinal);
            AddProductGroupChange(row, batchId, "identity", current.IdentityHash, hashes.IdentityHash, oldGroups["identity"], groups["identity"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "price", current.PriceHash, hashes.PriceHash, oldGroups["price"], groups["price"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "stock", current.StockHash, hashes.StockHash, oldGroups["stock"], groups["stock"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "rating", current.RatingHash, hashes.RatingHash, oldGroups["rating"], groups["rating"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "reviews", current.ReviewsHash, hashes.ReviewsHash, oldGroups["reviews"], groups["reviews"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "media", current.MediaHash, hashes.MediaHash, oldGroups["media"], groups["media"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "sellerBrand", current.SellerBrandHash, hashes.SellerBrandHash, oldGroups["sellerBrand"], groups["sellerBrand"], serverReceivedAtUtc);
            current.Apply(row, hashes, serverReceivedAtUtc);
            MarkProductSeen(current, row, context, serverReceivedAtUtc, oldPresenceStatus);
            if (hasChange)
            {
                RecordProductEffect(context, row.WbProductId, current.Id, ParserRunProductEffectTypes.Updated, serverReceivedAtUtc);
                RecordCurrentEntityEffect(
                    context,
                    row.WbProductId,
                    ParserRunCurrentEntityKinds.Product,
                    current.Id.ToString("D"),
                    ParserRunCurrentEntityEffectTypes.Updated,
                    oldSnapshot,
                    ParserRunCurrentEntitySnapshots.FromProduct(current),
                    serverReceivedAtUtc);
            }
        }

        await ApplyRankProjectionAsync(touchedCurrentRows, serverReceivedAtUtc, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private void MarkProductSeen(
        ParserCurrentProductRow current,
        ParserProductRow row,
        ParserCdcApplyContext? context,
        DateTime seenAtUtc,
        string? oldPresenceStatus = null)
    {
        var previousStatus = oldPresenceStatus ?? current.MarketplacePresenceStatus;
        var parserCycleId = NormalizePresenceCycleId(context?.ParserCycleId, row.ParserRunId);
        current.MarkSeen(parserCycleId, context?.ParserProxyRunId.ToString("D"), seenAtUtc);

        if (previousStatus == ParserMarketplacePresenceStatuses.MissingInLatestFullScan)
        {
            _dbContext.ParserProductPresenceEvents.Add(new ParserProductPresenceEvent(
                row.WbProductId,
                parserCycleId,
                row.SourceCategory,
                row.SourceSubcategory,
                ParserMarketplacePresenceStatuses.MissingInLatestFullScan,
                ParserMarketplacePresenceStatuses.Active,
                "seen_after_missing",
                seenAtUtc));
        }
    }

    public async Task ApplyLogisticsRowsAsync(
        IReadOnlyCollection<ParserLogisticsSnapshotRow> rows,
        string batchId,
        CancellationToken cancellationToken)
    {
        await ApplyLogisticsRowsAsync(rows, batchId, null, cancellationToken);
    }

    public async Task ApplyLogisticsRowsAsync(
        IReadOnlyCollection<ParserLogisticsSnapshotRow> rows,
        string batchId,
        ParserCdcApplyContext? context,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;

        var latestRows = rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(row => row.ObservedAtUtc).ThenByDescending(row => row.Id).First())
            .ToList();
        var ids = latestRows.Select(x => x.WbProductId).ToList();
        var currentRows = await _dbContext.ParserCurrentProductLogistics
            .Where(x => ids.Contains(x.WbProductId))
            .ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);
        var serverReceivedAtUtc = DateTime.UtcNow;

        foreach (var row in latestRows)
        {
            var value = LogisticsGroup(row);
            var hash = Hash(value);
            if (!currentRows.TryGetValue(row.WbProductId, out var current))
            {
                var created = new ParserCurrentProductLogistics(row.WbProductId, row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ObservedAtUtc, batchId);
                _dbContext.ParserCurrentProductLogistics.Add(created);
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "logistics", "created", null, hash, null, value, serverReceivedAtUtc);
                RecordCurrentEntityEffect(context, row.WbProductId, ParserRunCurrentEntityKinds.Logistics, created.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Created, null, ParserRunCurrentEntitySnapshots.FromLogistics(created), serverReceivedAtUtc);
                continue;
            }

            if (!string.Equals(current.LogisticsHash, hash, StringComparison.Ordinal))
            {
                var oldSnapshot = ParserRunCurrentEntitySnapshots.FromLogistics(current);
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "logistics", "updated", current.LogisticsHash, hash, current.LogisticsJson, value, serverReceivedAtUtc);
                current.Update(row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ObservedAtUtc, batchId);
                RecordCurrentEntityEffect(context, row.WbProductId, ParserRunCurrentEntityKinds.Logistics, current.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Updated, oldSnapshot, ParserRunCurrentEntitySnapshots.FromLogistics(current), serverReceivedAtUtc);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyProductDetailRowsAsync(IReadOnlyCollection<ParserProductDetailRow> rows, string batchId, CancellationToken cancellationToken)
    {
        await ApplyProductDetailRowsAsync(rows, batchId, null, cancellationToken);
    }

    public async Task ApplyProductDetailRowsAsync(
        IReadOnlyCollection<ParserProductDetailRow> rows,
        string batchId,
        ParserCdcApplyContext? context,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;
        var latestRows = rows.GroupBy(x => x.WbProductId, StringComparer.Ordinal).Select(x => x.OrderByDescending(row => row.ParsedAtUtc).First()).ToList();
        var ids = latestRows.Select(x => x.WbProductId).ToList();
        var currentRows = await _dbContext.ParserCurrentProductDetails.Where(x => ids.Contains(x.WbProductId)).ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);
        var serverReceivedAtUtc = DateTime.UtcNow;
        foreach (var row in latestRows)
        {
            var value = DetailGroup(row);
            var hash = Hash(value);
            if (!currentRows.TryGetValue(row.WbProductId, out var current))
            {
                var created = new ParserCurrentProductDetail(row.WbProductId, row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ParsedAtUtc, batchId);
                _dbContext.ParserCurrentProductDetails.Add(created);
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "details", "created", null, hash, null, value, serverReceivedAtUtc);
                RecordCurrentEntityEffect(context, row.WbProductId, ParserRunCurrentEntityKinds.Details, created.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Created, null, ParserRunCurrentEntitySnapshots.FromDetails(created), serverReceivedAtUtc);
            }
            else if (!string.Equals(current.DetailsHash, hash, StringComparison.Ordinal))
            {
                var oldSnapshot = ParserRunCurrentEntitySnapshots.FromDetails(current);
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "details", "updated", current.DetailsHash, hash, current.DetailsJson, value, serverReceivedAtUtc);
                current.Update(row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ParsedAtUtc, batchId);
                RecordCurrentEntityEffect(context, row.WbProductId, ParserRunCurrentEntityKinds.Details, current.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Updated, oldSnapshot, ParserRunCurrentEntitySnapshots.FromDetails(current), serverReceivedAtUtc);
            }
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyRankProjectionForRankScopeAsync(
        IReadOnlyCollection<ParserRankSnapshotRow> rows,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var sourceSubcategories = rows
            .Select(x => x.SourceSubcategory)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (sourceSubcategories.Count == 0)
            return;

        var sourceCategories = rows
            .Select(x => x.SourceCategory)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var productsQuery = _dbContext.ParserCurrentProductRows
            .Where(x => x.SourceSubcategory != null && sourceSubcategories.Contains(x.SourceSubcategory));
        if (sourceCategories.Count > 0)
            productsQuery = productsQuery.Where(x => x.SourceCategory != null && sourceCategories.Contains(x.SourceCategory));

        var products = await productsQuery.ToListAsync(cancellationToken);
        await ApplyRankProjectionAsync(products, updatedAtUtc, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyRankProjectionAsync(
        IReadOnlyCollection<ParserCurrentProductRow> products,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
            return;

        var wbProductIds = products
            .Select(x => x.WbProductId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var wbRootIds = products
            .Select(x => x.WbRootId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var sourceSubcategories = products
            .Select(x => x.SourceSubcategory)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var ranks = await _dbContext.ParserCurrentProductRanks
            .Where(x =>
                wbProductIds.Contains(x.WbProductId) ||
                (x.WbRootId != null && wbRootIds.Contains(x.WbRootId)) ||
                (x.SourceSubcategory != null && sourceSubcategories.Contains(x.SourceSubcategory)))
            .ToListAsync(cancellationToken);

        var candidates = ranks
            .Select(RankPositionCandidate.FromCurrentRank)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
        var exactByProductId = candidates
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(candidate => candidate.AbsolutePosition).ThenByDescending(candidate => candidate.ObservedAtUtc).First(),
                StringComparer.Ordinal);
        var uniqueByRootId = candidates
            .Where(x => !string.IsNullOrWhiteSpace(x.WbRootId))
            .GroupBy(x => x.WbRootId!, StringComparer.Ordinal)
            .Where(x => x.Select(candidate => candidate.WbProductId).Distinct(StringComparer.Ordinal).Count() == 1)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(candidate => candidate.AbsolutePosition).ThenByDescending(candidate => candidate.ObservedAtUtc).First(),
                StringComparer.Ordinal);
        var coverageByKey = candidates
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceSubcategory))
            .GroupBy(x => new PositionCoverageKey(x.SourceCategory, x.SourceSubcategory, x.SourceRegionDest))
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var queries = x.Select(candidate => candidate.Query)
                        .Where(query => !string.IsNullOrWhiteSpace(query))
                        .Distinct(StringComparer.Ordinal)
                        .Take(2)
                        .ToList();
                    return new PositionCoverage(
                        x.Max(candidate => candidate.AbsolutePosition),
                        queries.Count == 1 ? queries[0] : null,
                        x.Max(candidate => candidate.ObservedAtUtc));
                });

        foreach (var product in products)
        {
            if (exactByProductId.TryGetValue(product.WbProductId, out var exact)
                || (!string.IsNullOrWhiteSpace(product.WbRootId) && uniqueByRootId.TryGetValue(product.WbRootId!, out exact)))
            {
                product.ApplyPosition(
                    PositionStateObserved,
                    exact.AbsolutePosition,
                    null,
                    exact.Query,
                    exact.ObservedAtUtc,
                    updatedAtUtc);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(product.SourceSubcategory)
                && coverageByKey.TryGetValue(
                    new PositionCoverageKey(product.SourceCategory, product.SourceSubcategory, product.SourceRegionDest),
                    out var coverage))
            {
                product.ApplyPosition(
                    PositionStateBeyondObservedRange,
                    null,
                    coverage.ObservedRangeLimit,
                    coverage.Query ?? product.SourceQuery,
                    coverage.ObservedAtUtc,
                    updatedAtUtc);
                continue;
            }

            product.ApplyPosition(
                PositionStateUnknown,
                null,
                null,
                product.SourceQuery,
                null,
                updatedAtUtc);
        }
    }

    public async Task ApplyRankRowsAsync(IReadOnlyCollection<ParserRankSnapshotRow> rows, string batchId, CancellationToken cancellationToken)
    {
        await ApplyRankRowsAsync(rows, batchId, null, cancellationToken);
    }

    public async Task ApplyRankRowsAsync(
        IReadOnlyCollection<ParserRankSnapshotRow> rows,
        string batchId,
        ParserCdcApplyContext? context,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;
        var latestRows = rows.GroupBy(x => x.WbProductId, StringComparer.Ordinal).Select(x => x.OrderBy(row => row.AbsolutePosition).ThenByDescending(row => row.ObservedAtUtc).First()).ToList();
        var ids = latestRows.Select(x => x.WbProductId).ToList();
        var currentRows = await _dbContext.ParserCurrentProductRanks.Where(x => ids.Contains(x.WbProductId)).ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);
        var serverReceivedAtUtc = DateTime.UtcNow;
        foreach (var row in latestRows)
        {
            var value = RankGroup(row);
            var hash = Hash(value);
            if (!currentRows.TryGetValue(row.WbProductId, out var current))
            {
                var created = new ParserCurrentProductRank(row.WbProductId, row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ObservedAtUtc, batchId);
                _dbContext.ParserCurrentProductRanks.Add(created);
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "rank", "created", null, hash, null, value, serverReceivedAtUtc);
                RecordCurrentEntityEffect(context, row.WbProductId, ParserRunCurrentEntityKinds.Rank, created.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Created, null, ParserRunCurrentEntitySnapshots.FromRank(created), serverReceivedAtUtc);
            }
            else if (!string.Equals(current.RankHash, hash, StringComparison.Ordinal))
            {
                var oldSnapshot = ParserRunCurrentEntitySnapshots.FromRank(current);
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "rank", "updated", current.RankHash, hash, current.RankJson, value, serverReceivedAtUtc);
                current.Update(row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ObservedAtUtc, batchId);
                RecordCurrentEntityEffect(context, row.WbProductId, ParserRunCurrentEntityKinds.Rank, current.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Updated, oldSnapshot, ParserRunCurrentEntitySnapshots.FromRank(current), serverReceivedAtUtc);
            }
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        await ApplyRankProjectionForRankScopeAsync(rows, serverReceivedAtUtc, cancellationToken);
    }

    public async Task ApplyReviewRowsAsync(IReadOnlyCollection<ParserReviewRow> rows, string batchId, CancellationToken cancellationToken)
    {
        await ApplyReviewRowsAsync(rows, batchId, null, cancellationToken);
    }

    public async Task ApplyReviewRowsAsync(
        IReadOnlyCollection<ParserReviewRow> rows,
        string batchId,
        ParserCdcApplyContext? context,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;

        var productIds = rows.Select(x => x.WbProductId).Distinct(StringComparer.Ordinal).ToList();
        var currentProducts = await _dbContext.ParserCurrentProductRows
            .Where(x => productIds.Contains(x.WbProductId))
            .ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);
        var summaries = await _dbContext.ParserCurrentProductReviewsSummaries
            .Where(x => productIds.Contains(x.WbProductId))
            .ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);
        var evidenceRows = await _dbContext.ParserCurrentProductReviewEvidence
            .Where(x => productIds.Contains(x.WbProductId))
            .ToDictionaryAsync(x => ReviewKey(x.WbProductId, x.ReviewIdOnMp), StringComparer.Ordinal, cancellationToken);
        var summaryStates = summaries.ToDictionary(
            x => x.Key,
            x => ReviewSummaryState.FromCurrent(x.Value),
            StringComparer.Ordinal);
        var touchedProducts = new Dictionary<string, ParserReviewRow>(StringComparer.Ordinal);
        var serverReceivedAtUtc = DateTime.UtcNow;

        foreach (var row in rows)
        {
            var value = ReviewGroup(row);
            var hash = Hash(value);
            var key = ReviewKey(row.WbProductId, row.ReviewIdOnMp);
            if (!summaryStates.TryGetValue(row.WbProductId, out var state))
            {
                state = ReviewSummaryState.Empty();
                summaryStates[row.WbProductId] = state;
            }

            if (!evidenceRows.TryGetValue(key, out var evidence))
            {
                evidence = new ParserCurrentProductReviewEvidence(row.WbProductId, row.SourceWbRootId, row.ReviewIdOnMp, hash, ToJson(value), row.Rating, row.CreatedAtOnMp, row.ParsedAtUtc, batchId);
                _dbContext.ParserCurrentProductReviewEvidence.Add(evidence);
                evidenceRows[key] = evidence;
                AddEvent(row.WbProductId, row.SourceWbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "reviews", "review_created", null, hash, null, value, serverReceivedAtUtc);
                RecordCurrentEntityEffect(context, row.WbProductId, ParserRunCurrentEntityKinds.ReviewEvidence, evidence.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Created, null, ParserRunCurrentEntitySnapshots.FromReviewEvidence(evidence), serverReceivedAtUtc);
                state.Add(row.Rating, row.CreatedAtOnMp);
                touchedProducts[row.WbProductId] = row;
                continue;
            }

            if (string.Equals(evidence.ReviewHash, hash, StringComparison.Ordinal))
                continue;

            AddEvent(row.WbProductId, row.SourceWbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "reviews", "review_updated", evidence.ReviewHash, hash, evidence.ReviewJson, value, serverReceivedAtUtc);
            var oldEvidenceSnapshot = ParserRunCurrentEntitySnapshots.FromReviewEvidence(evidence);
            state.Replace(evidence.Rating, row.Rating, evidence.CreatedAtOnMp, row.CreatedAtOnMp);
            evidence.Update(row.SourceWbRootId, hash, ToJson(value), row.Rating, row.CreatedAtOnMp, row.ParsedAtUtc, batchId);
            RecordCurrentEntityEffect(context, row.WbProductId, ParserRunCurrentEntityKinds.ReviewEvidence, evidence.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Updated, oldEvidenceSnapshot, ParserRunCurrentEntitySnapshots.FromReviewEvidence(evidence), serverReceivedAtUtc);
            touchedProducts[row.WbProductId] = row;
        }

        foreach (var (productId, first) in touchedProducts)
        {
            var state = summaryStates[productId];
            currentProducts.TryGetValue(productId, out var currentProduct);
            state.ApplyCoverage(currentProduct?.FeedbackCount, first.ReviewAttributionMode);
            var value = state.ToValue();
            var hash = Hash(value);
            if (!summaries.TryGetValue(productId, out var current))
            {
                var created = new ParserCurrentProductReviewsSummary(
                    first.WbProductId,
                    first.SourceWbRootId,
                    first.SourceCategory,
                    first.SourceSubcategory,
                    state.ReviewsCount,
                    state.AverageRating,
                    state.RecentNegativeCount,
                    state.LastReviewDateUtc,
                    state.MarketplaceFeedbackCount,
                    state.FetchedReviewsCount,
                    state.OldestReviewDateUtc,
                    state.CoverageStatus,
                    state.CoverageSource,
                    state.LastCoverageError,
                    hash,
                    ToJson(value),
                    first.ParsedAtUtc,
                    batchId);
                _dbContext.ParserCurrentProductReviewsSummaries.Add(created);
                RecordCurrentEntityEffect(context, first.WbProductId, ParserRunCurrentEntityKinds.ReviewsSummary, created.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Created, null, ParserRunCurrentEntitySnapshots.FromReviewsSummary(created), serverReceivedAtUtc);
            }
            else if (!string.Equals(current.ReviewsHash, hash, StringComparison.Ordinal))
            {
                var oldSummarySnapshot = ParserRunCurrentEntitySnapshots.FromReviewsSummary(current);
                current.Update(
                    state.ReviewsCount,
                    state.AverageRating,
                    state.RecentNegativeCount,
                    state.LastReviewDateUtc,
                    state.MarketplaceFeedbackCount,
                    state.FetchedReviewsCount,
                    state.OldestReviewDateUtc,
                    state.CoverageStatus,
                    state.CoverageSource,
                    state.LastCoverageError,
                    hash,
                    ToJson(value),
                    first.ParsedAtUtc,
                    batchId);
                RecordCurrentEntityEffect(context, first.WbProductId, ParserRunCurrentEntityKinds.ReviewsSummary, current.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Updated, oldSummarySnapshot, ParserRunCurrentEntitySnapshots.FromReviewsSummary(current), serverReceivedAtUtc);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyReviewCoverageRowsAsync(
        IReadOnlyCollection<ParserReviewCoverageSnapshot> rows,
        string batchId,
        CancellationToken cancellationToken)
    {
        await ApplyReviewCoverageRowsAsync(rows, batchId, null, cancellationToken);
    }

    public async Task ApplyReviewCoverageRowsAsync(
        IReadOnlyCollection<ParserReviewCoverageSnapshot> rows,
        string batchId,
        ParserCdcApplyContext? context,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;

        var productIds = rows
            .Select(x => x.WbProductId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (productIds.Count == 0)
            return;

        var currentProducts = await _dbContext.ParserCurrentProductRows
            .Where(x => productIds.Contains(x.WbProductId))
            .ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);
        var summaries = await _dbContext.ParserCurrentProductReviewsSummaries
            .Where(x => productIds.Contains(x.WbProductId))
            .ToDictionaryAsync(x => x.WbProductId, StringComparer.Ordinal, cancellationToken);
        var serverReceivedAtUtc = DateTime.UtcNow;

        foreach (var row in rows)
        {
            currentProducts.TryGetValue(row.WbProductId, out var currentProduct);
            summaries.TryGetValue(row.WbProductId, out var current);
            var reviewsCount = current?.ReviewsCount ?? row.FetchedReviewsCount;
            var averageRating = current?.AverageRating;
            var recentNegativeCount = current?.RecentNegativeCount ?? 0;
            var lastReviewDateUtc = current?.LastReviewDateUtc;
            var oldestReviewDateUtc = current?.OldestReviewDateUtc;
            var wbRootId = row.WbRootId ?? current?.WbRootId ?? currentProduct?.WbRootId;
            var sourceCategory = current?.SourceCategory ?? currentProduct?.SourceCategory;
            var sourceSubcategory = current?.SourceSubcategory ?? currentProduct?.SourceSubcategory;
            var observedAtUtc = row.ObservedAtUtc.Kind == DateTimeKind.Utc
                ? row.ObservedAtUtc
                : DateTime.SpecifyKind(row.ObservedAtUtc, DateTimeKind.Utc);

            var value = ReviewCoverageValue(
                reviewsCount,
                averageRating,
                recentNegativeCount,
                lastReviewDateUtc,
                oldestReviewDateUtc,
                row.MarketplaceFeedbackCount,
                row.FetchedReviewsCount,
                row.CoverageStatus,
                row.CoverageSource,
                row.LastCoverageError);
            var hash = Hash(value);

            if (current is null)
            {
                var created = new ParserCurrentProductReviewsSummary(
                    row.WbProductId,
                    wbRootId,
                    sourceCategory,
                    sourceSubcategory,
                    reviewsCount,
                    averageRating,
                    recentNegativeCount,
                    lastReviewDateUtc,
                    row.MarketplaceFeedbackCount,
                    row.FetchedReviewsCount,
                    oldestReviewDateUtc,
                    row.CoverageStatus,
                    row.CoverageSource,
                    row.LastCoverageError,
                    hash,
                    ToJson(value),
                    observedAtUtc,
                    batchId);
                _dbContext.ParserCurrentProductReviewsSummaries.Add(created);
                RecordCurrentEntityEffect(context, row.WbProductId, ParserRunCurrentEntityKinds.ReviewsSummary, created.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Created, null, ParserRunCurrentEntitySnapshots.FromReviewsSummary(created), serverReceivedAtUtc);
                summaries[row.WbProductId] = created;
                continue;
            }

            if (string.Equals(current.ReviewsHash, hash, StringComparison.Ordinal))
                continue;

            var oldSummarySnapshot = ParserRunCurrentEntitySnapshots.FromReviewsSummary(current);
            current.Update(
                reviewsCount,
                averageRating,
                recentNegativeCount,
                lastReviewDateUtc,
                row.MarketplaceFeedbackCount,
                row.FetchedReviewsCount,
                oldestReviewDateUtc,
                row.CoverageStatus,
                row.CoverageSource,
                row.LastCoverageError,
                hash,
                ToJson(value),
                observedAtUtc,
                batchId);
            RecordCurrentEntityEffect(context, row.WbProductId, ParserRunCurrentEntityKinds.ReviewsSummary, current.Id.ToString("D"), ParserRunCurrentEntityEffectTypes.Updated, oldSummarySnapshot, ParserRunCurrentEntitySnapshots.FromReviewsSummary(current), serverReceivedAtUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private void RecordProductEffect(
        ParserCdcApplyContext? context,
        string wbProductId,
        Guid serviceProductId,
        string effectType,
        DateTime createdAtUtc)
    {
        if (context is null)
            return;

        _dbContext.ParserRunProductEffects.Add(new ParserRunProductEffect(
            context.ParserProxyRunId,
            context.ParserBatchSubmissionId,
            wbProductId,
            serviceProductId,
            effectType,
            createdAtUtc));
    }

    private void RecordCurrentEntityEffect(
        ParserCdcApplyContext? context,
        string wbProductId,
        string entityKind,
        string entityKey,
        string effectType,
        object? oldValue,
        object? newValue,
        DateTime createdAtUtc)
    {
        if (context is null)
            return;

        _dbContext.ParserRunCurrentEntityEffects.Add(new ParserRunCurrentEntityEffect(
            context.ParserProxyRunId,
            context.ParserBatchSubmissionId,
            wbProductId,
            entityKind,
            entityKey,
            effectType,
            oldValue is null ? null : ToJson(oldValue),
            newValue is null ? null : ToJson(newValue),
            createdAtUtc));
    }

    private void AddProductGroupChange(ParserProductRow row, string batchId, string fieldGroup, string? oldHash, string newHash, object oldValue, object newValue, DateTime serverReceivedAtUtc)
    {
        if (string.Equals(oldHash, newHash, StringComparison.Ordinal))
            return;
        AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, fieldGroup, "updated", oldHash, newHash, ToJson(oldValue), newValue, serverReceivedAtUtc);
    }

    private void AddEvent(string wbProductId, string? wbRootId, string batchId, string? sourceCategory, string? sourceSubcategory, string fieldGroup, string changeType, string? oldHash, string newHash, string? oldValue, object? newValue, DateTime observedAtUtc)
    {
        _dbContext.ParserProductChangeEvents.Add(new ParserProductChangeEvent(
            wbProductId,
            wbRootId,
            batchId,
            sourceCategory,
            sourceSubcategory,
            fieldGroup,
            changeType,
            oldHash,
            newHash,
            oldValue,
            newValue is null ? null : ToJson(newValue),
            observedAtUtc));
    }

    private static Dictionary<string, object> ProductGroups(ParserProductRow row) => new(StringComparer.Ordinal)
    {
        ["identity"] = new SortedDictionary<string, object?> { ["wbProductId"] = row.WbProductId, ["wbRootId"] = row.WbRootId, ["name"] = row.Name, ["sourceCategory"] = row.SourceCategory, ["sourceSubcategory"] = row.SourceSubcategory, ["sourceRegionDest"] = row.SourceRegionDest },
        ["price"] = new SortedDictionary<string, object?> { ["priceRegular"] = row.PriceRegular, ["priceDiscounted"] = row.PriceDiscounted, ["priceWbWallet"] = row.PriceWbWallet, ["discountPercent"] = row.DiscountPercent },
        ["stock"] = new SortedDictionary<string, object?> { ["totalQuantity"] = row.TotalQuantity },
        ["rating"] = new SortedDictionary<string, object?> { ["ratingRounded"] = row.RatingRounded, ["reviewRating"] = row.ReviewRating },
        ["reviews"] = new SortedDictionary<string, object?> { ["feedbackCount"] = row.FeedbackCount, ["feedbackCountSource"] = row.FeedbackCountSource },
        ["media"] = new SortedDictionary<string, object?> { ["imageCount"] = row.ImageCount, ["imageUrls"] = JsonText(row.ImageUrls) },
        ["sellerBrand"] = new SortedDictionary<string, object?> { ["brandIdOnMp"] = row.BrandIdOnMp, ["brandName"] = row.BrandName, ["sellerIdOnMp"] = row.SellerIdOnMp, ["sellerName"] = row.SellerName }
    };

    private static Dictionary<string, object> ProductGroups(ParserCurrentProductRow row) => new(StringComparer.Ordinal)
    {
        ["identity"] = new SortedDictionary<string, object?> { ["wbProductId"] = row.WbProductId, ["wbRootId"] = row.WbRootId, ["name"] = row.Name, ["sourceCategory"] = row.SourceCategory, ["sourceSubcategory"] = row.SourceSubcategory, ["sourceRegionDest"] = row.SourceRegionDest },
        ["price"] = new SortedDictionary<string, object?> { ["priceRegular"] = row.PriceRegular, ["priceDiscounted"] = row.PriceDiscounted, ["priceWbWallet"] = row.PriceWbWallet, ["discountPercent"] = row.DiscountPercent },
        ["stock"] = new SortedDictionary<string, object?> { ["totalQuantity"] = row.TotalQuantity },
        ["rating"] = new SortedDictionary<string, object?> { ["ratingRounded"] = row.RatingRounded, ["reviewRating"] = row.ReviewRating },
        ["reviews"] = new SortedDictionary<string, object?> { ["feedbackCount"] = row.FeedbackCount, ["feedbackCountSource"] = row.FeedbackCountSource },
        ["media"] = new SortedDictionary<string, object?> { ["imageCount"] = row.ImageCount, ["imageUrls"] = row.ImageUrlsJson },
        ["sellerBrand"] = new SortedDictionary<string, object?> { ["brandIdOnMp"] = row.BrandIdOnMp, ["brandName"] = row.BrandName, ["sellerIdOnMp"] = row.SellerIdOnMp, ["sellerName"] = row.SellerName }
    };

    private static ProductGroupHashes ProductHashes(Dictionary<string, object> groups) => new(
        Hash(groups["identity"]),
        Hash(groups["price"]),
        Hash(groups["stock"]),
        Hash(groups["rating"]),
        Hash(groups["reviews"]),
        Hash(groups["media"]),
        Hash(groups["sellerBrand"]));

    private static SortedDictionary<string, object?> LogisticsGroup(ParserLogisticsSnapshotRow row) => new(StringComparer.Ordinal)
    {
        ["sourceRegionDest"] = row.SourceRegionDest,
        ["deliveryProfileKey"] = row.DeliveryProfileKey,
        ["totalQuantityObserved"] = row.TotalQuantityObserved,
        ["quantitySemantics"] = row.QuantitySemantics,
        ["visibleDeliveryStatus"] = row.VisibleDeliveryStatus,
        ["visibleDeliveryLabel"] = row.VisibleDeliveryLabel,
        ["visibleDeliveryDate"] = row.VisibleDeliveryDate,
        ["productWhRaw"] = row.ProductWhRaw,
        ["productTime1Raw"] = row.ProductTime1Raw,
        ["productTime2Raw"] = row.ProductTime2Raw
    };

    private static SortedDictionary<string, object?> DetailGroup(ParserProductDetailRow row) => new(StringComparer.Ordinal)
    {
        ["description"] = row.Description,
        ["characteristics"] = JsonText(row.Characteristics),
        ["groupedOptions"] = JsonText(row.GroupedOptions),
        ["mediaCount"] = row.MediaCount,
        ["status"] = row.Status
    };

    private static SortedDictionary<string, object?> RankGroup(ParserRankSnapshotRow row) => new(StringComparer.Ordinal)
    {
        ["rankContextId"] = row.RankContextId,
        ["query"] = row.Query,
        ["sourceRegionDest"] = row.SourceRegionDest,
        ["sort"] = row.Sort,
        ["absolutePosition"] = row.AbsolutePosition,
        ["page"] = row.Page,
        ["positionOnPage"] = row.PositionOnPage,
        ["responseTotal"] = row.ResponseTotal,
        ["fetchStatus"] = row.FetchStatus
    };

    private static SortedDictionary<string, object?> ReviewGroup(ParserReviewRow row) => new(StringComparer.Ordinal)
    {
        ["reviewIdOnMp"] = row.ReviewIdOnMp,
        ["rating"] = row.Rating,
        ["text"] = row.Text,
        ["pros"] = row.Pros,
        ["cons"] = row.Cons,
        ["createdAtOnMp"] = row.CreatedAtOnMp,
        ["reviewerName"] = row.ReviewerName,
        ["reviewerCountry"] = row.ReviewerCountry,
        ["reviewerHasPhoto"] = row.ReviewerHasPhoto,
        ["helpfulPlus"] = row.HelpfulPlus,
        ["helpfulMinus"] = row.HelpfulMinus,
        ["isPartialSnapshot"] = row.IsPartialSnapshot,
        ["isCappedRootPayload"] = row.IsCappedRootPayload,
        ["isFullHistoryUnknown"] = row.IsFullHistoryUnknown
    };

    private static string ReviewKey(string wbProductId, string reviewIdOnMp) => $"{wbProductId}\u001f{reviewIdOnMp}";

    private static SortedDictionary<string, object?> ReviewCoverageValue(
        int reviewsCount,
        decimal? averageRating,
        int recentNegativeCount,
        DateTime? lastReviewDateUtc,
        DateTime? oldestReviewDateUtc,
        int? marketplaceFeedbackCount,
        int fetchedReviewsCount,
        string coverageStatus,
        string coverageSource,
        string? lastCoverageError) => new(StringComparer.Ordinal)
    {
        ["reviewsCount"] = reviewsCount,
        ["averageRating"] = averageRating,
        ["recentNegativeCount"] = recentNegativeCount,
        ["lastReviewDateUtc"] = lastReviewDateUtc,
        ["oldestReviewDateUtc"] = oldestReviewDateUtc,
        ["marketplaceFeedbackCount"] = marketplaceFeedbackCount,
        ["fetchedReviewsCount"] = fetchedReviewsCount,
        ["coverageStatus"] = coverageStatus,
        ["coverageSource"] = coverageSource,
        ["lastCoverageError"] = lastCoverageError
    };

    private static string Hash(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static string ToJson(object value) => JsonSerializer.Serialize(value, JsonOptions);

    private static string? JsonText(JsonDocument? value) => value?.RootElement.GetRawText();

    private static string NormalizePresenceCycleId(string? parserCycleId, string parserRunId)
    {
        if (!string.IsNullOrWhiteSpace(parserCycleId))
            return parserCycleId.Trim();
        if (!string.IsNullOrWhiteSpace(parserRunId))
            return parserRunId.Trim();
        return "legacy";
    }

    private sealed record PositionCoverageKey(string? SourceCategory, string? SourceSubcategory, string? SourceRegionDest);

    private sealed record PositionCoverage(int ObservedRangeLimit, string? Query, DateTime ObservedAtUtc);

    private sealed record RankPositionCandidate(
        string WbProductId,
        string? WbRootId,
        string? SourceCategory,
        string? SourceSubcategory,
        string? SourceRegionDest,
        string Query,
        int AbsolutePosition,
        DateTime ObservedAtUtc)
    {
        public static RankPositionCandidate? FromCurrentRank(ParserCurrentProductRank row)
        {
            try
            {
                using var document = JsonDocument.Parse(row.RankJson);
                var root = document.RootElement;
                var absolutePosition = root.TryGetProperty("absolutePosition", out var absoluteValue)
                    && absoluteValue.ValueKind == JsonValueKind.Number
                    && absoluteValue.TryGetInt32(out var parsedAbsolute)
                        ? parsedAbsolute
                        : 0;
                if (absolutePosition <= 0)
                    return null;

                var query = root.TryGetProperty("query", out var queryValue) && queryValue.ValueKind == JsonValueKind.String
                    ? queryValue.GetString() ?? string.Empty
                    : string.Empty;
                var sourceRegionDest = root.TryGetProperty("sourceRegionDest", out var destValue) && destValue.ValueKind == JsonValueKind.String
                    ? destValue.GetString()
                    : null;

                return new RankPositionCandidate(
                    row.WbProductId,
                    row.WbRootId,
                    row.SourceCategory,
                    row.SourceSubcategory,
                    sourceRegionDest,
                    query,
                    absolutePosition,
                    row.ObservedAtUtc);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    private sealed class ReviewSummaryState
    {
        private int _ratedCount;
        private decimal _ratingSum;

        private ReviewSummaryState(
            int reviewsCount,
            int ratedCount,
            decimal ratingSum,
            int recentNegativeCount,
            DateTime? lastReviewDateUtc,
            DateTime? oldestReviewDateUtc,
            int? marketplaceFeedbackCount,
            int fetchedReviewsCount,
            string coverageStatus,
            string coverageSource,
            string? lastCoverageError)
        {
            ReviewsCount = reviewsCount;
            _ratedCount = ratedCount;
            _ratingSum = ratingSum;
            RecentNegativeCount = recentNegativeCount;
            LastReviewDateUtc = lastReviewDateUtc;
            OldestReviewDateUtc = oldestReviewDateUtc;
            MarketplaceFeedbackCount = marketplaceFeedbackCount;
            FetchedReviewsCount = fetchedReviewsCount;
            CoverageStatus = coverageStatus;
            CoverageSource = coverageSource;
            LastCoverageError = lastCoverageError;
        }

        public int ReviewsCount { get; private set; }
        public decimal? AverageRating => _ratedCount == 0 ? null : _ratingSum / _ratedCount;
        public int RecentNegativeCount { get; private set; }
        public DateTime? LastReviewDateUtc { get; private set; }
        public DateTime? OldestReviewDateUtc { get; private set; }
        public int? MarketplaceFeedbackCount { get; private set; }
        public int FetchedReviewsCount { get; private set; }
        public string CoverageStatus { get; private set; }
        public string CoverageSource { get; private set; }
        public string? LastCoverageError { get; private set; }

        public static ReviewSummaryState Empty() => new(0, 0, 0, 0, null, null, null, 0, "unknown", "root_capped_fallback", null);

        public static ReviewSummaryState FromCurrent(ParserCurrentProductReviewsSummary current)
        {
            var ratedCount = current.AverageRating.HasValue && current.ReviewsCount > 0 ? current.ReviewsCount : 0;
            var ratingSum = (current.AverageRating ?? 0) * ratedCount;
            return new ReviewSummaryState(
                current.ReviewsCount,
                ratedCount,
                ratingSum,
                current.RecentNegativeCount,
                current.LastReviewDateUtc,
                current.OldestReviewDateUtc,
                current.MarketplaceFeedbackCount,
                current.FetchedReviewsCount,
                current.CoverageStatus,
                current.CoverageSource,
                current.LastCoverageError);
        }

        public void Add(int? rating, DateTime? createdAtOnMp)
        {
            ReviewsCount++;
            if (rating.HasValue)
            {
                _ratedCount++;
                _ratingSum += rating.Value;
                if (rating.Value <= 3)
                    RecentNegativeCount++;
            }

            if (createdAtOnMp.HasValue && (!LastReviewDateUtc.HasValue || createdAtOnMp.Value > LastReviewDateUtc.Value))
                LastReviewDateUtc = createdAtOnMp.Value;
            if (createdAtOnMp.HasValue && (!OldestReviewDateUtc.HasValue || createdAtOnMp.Value < OldestReviewDateUtc.Value))
                OldestReviewDateUtc = createdAtOnMp.Value;
        }

        public void Replace(int? oldRating, int? newRating, DateTime? oldCreatedAtOnMp, DateTime? newCreatedAtOnMp)
        {
            if (oldRating.HasValue)
            {
                _ratedCount = Math.Max(0, _ratedCount - 1);
                _ratingSum -= oldRating.Value;
                if (oldRating.Value <= 3)
                    RecentNegativeCount = Math.Max(0, RecentNegativeCount - 1);
            }

            if (newRating.HasValue)
            {
                _ratedCount++;
                _ratingSum += newRating.Value;
                if (newRating.Value <= 3)
                    RecentNegativeCount++;
            }

            if (newCreatedAtOnMp.HasValue && (!LastReviewDateUtc.HasValue || newCreatedAtOnMp.Value > LastReviewDateUtc.Value))
                LastReviewDateUtc = newCreatedAtOnMp.Value;
            if (newCreatedAtOnMp.HasValue && (!OldestReviewDateUtc.HasValue || newCreatedAtOnMp.Value < OldestReviewDateUtc.Value))
                OldestReviewDateUtc = newCreatedAtOnMp.Value;
        }

        public void ApplyCoverage(int? marketplaceFeedbackCount, string? attributionMode)
        {
            MarketplaceFeedbackCount = marketplaceFeedbackCount;
            FetchedReviewsCount = ReviewsCount;
            CoverageSource = string.Equals(attributionMode, "product_full", StringComparison.OrdinalIgnoreCase)
                ? "product_full"
                : "root_capped_fallback";

            if (!marketplaceFeedbackCount.HasValue)
            {
                CoverageStatus = "unknown";
                LastCoverageError = null;
            }
            else if (marketplaceFeedbackCount.Value == 0 && ReviewsCount == 0)
            {
                CoverageStatus = "full";
                LastCoverageError = null;
            }
            else if (ReviewsCount >= marketplaceFeedbackCount.Value)
            {
                CoverageStatus = "full";
                LastCoverageError = null;
            }
            else
            {
                CoverageStatus = "incomplete";
                LastCoverageError = $"Fetched {ReviewsCount} of {marketplaceFeedbackCount.Value} marketplace feedbacks.";
            }
        }

        public SortedDictionary<string, object?> ToValue() => new(StringComparer.Ordinal)
        {
            ["reviewsCount"] = ReviewsCount,
            ["averageRating"] = AverageRating,
            ["recentNegativeCount"] = RecentNegativeCount,
            ["lastReviewDateUtc"] = LastReviewDateUtc,
            ["oldestReviewDateUtc"] = OldestReviewDateUtc,
            ["marketplaceFeedbackCount"] = MarketplaceFeedbackCount,
            ["fetchedReviewsCount"] = FetchedReviewsCount,
            ["coverageStatus"] = CoverageStatus,
            ["coverageSource"] = CoverageSource,
            ["lastCoverageError"] = LastCoverageError
        };
    }
}
