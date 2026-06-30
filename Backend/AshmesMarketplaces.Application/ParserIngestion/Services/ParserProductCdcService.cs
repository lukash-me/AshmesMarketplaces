using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserIngestion.Services;

public sealed class ParserProductCdcService
{
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

        foreach (var row in latestRows)
        {
            var groups = ProductGroups(row);
            var hashes = ProductHashes(groups);
            if (!currentRows.TryGetValue(row.WbProductId, out var current))
            {
                _dbContext.ParserCurrentProductRows.Add(new ParserCurrentProductRow(row, hashes, serverReceivedAtUtc));
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "identity", "created", null, hashes.IdentityHash, null, groups["identity"], serverReceivedAtUtc);
                continue;
            }

            var oldGroups = ProductGroups(current);
            AddProductGroupChange(row, batchId, "identity", current.IdentityHash, hashes.IdentityHash, oldGroups["identity"], groups["identity"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "price", current.PriceHash, hashes.PriceHash, oldGroups["price"], groups["price"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "stock", current.StockHash, hashes.StockHash, oldGroups["stock"], groups["stock"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "rating", current.RatingHash, hashes.RatingHash, oldGroups["rating"], groups["rating"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "reviews", current.ReviewsHash, hashes.ReviewsHash, oldGroups["reviews"], groups["reviews"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "media", current.MediaHash, hashes.MediaHash, oldGroups["media"], groups["media"], serverReceivedAtUtc);
            AddProductGroupChange(row, batchId, "sellerBrand", current.SellerBrandHash, hashes.SellerBrandHash, oldGroups["sellerBrand"], groups["sellerBrand"], serverReceivedAtUtc);
            current.Apply(row, hashes, serverReceivedAtUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyLogisticsRowsAsync(
        IReadOnlyCollection<ParserLogisticsSnapshotRow> rows,
        string batchId,
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
                _dbContext.ParserCurrentProductLogistics.Add(new ParserCurrentProductLogistics(row.WbProductId, row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ObservedAtUtc, batchId));
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "logistics", "created", null, hash, null, value, serverReceivedAtUtc);
                continue;
            }

            if (!string.Equals(current.LogisticsHash, hash, StringComparison.Ordinal))
            {
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "logistics", "updated", current.LogisticsHash, hash, current.LogisticsJson, value, serverReceivedAtUtc);
                current.Update(row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ObservedAtUtc, batchId);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyProductDetailRowsAsync(IReadOnlyCollection<ParserProductDetailRow> rows, string batchId, CancellationToken cancellationToken)
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
                _dbContext.ParserCurrentProductDetails.Add(new ParserCurrentProductDetail(row.WbProductId, row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ParsedAtUtc, batchId));
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "details", "created", null, hash, null, value, serverReceivedAtUtc);
            }
            else if (!string.Equals(current.DetailsHash, hash, StringComparison.Ordinal))
            {
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "details", "updated", current.DetailsHash, hash, current.DetailsJson, value, serverReceivedAtUtc);
                current.Update(row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ParsedAtUtc, batchId);
            }
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyRankRowsAsync(IReadOnlyCollection<ParserRankSnapshotRow> rows, string batchId, CancellationToken cancellationToken)
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
                _dbContext.ParserCurrentProductRanks.Add(new ParserCurrentProductRank(row.WbProductId, row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ObservedAtUtc, batchId));
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "rank", "created", null, hash, null, value, serverReceivedAtUtc);
            }
            else if (!string.Equals(current.RankHash, hash, StringComparison.Ordinal))
            {
                AddEvent(row.WbProductId, row.WbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "rank", "updated", current.RankHash, hash, current.RankJson, value, serverReceivedAtUtc);
                current.Update(row.WbRootId, row.SourceCategory, row.SourceSubcategory, hash, ToJson(value), row.ObservedAtUtc, batchId);
            }
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyReviewRowsAsync(IReadOnlyCollection<ParserReviewRow> rows, string batchId, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;

        var productIds = rows.Select(x => x.WbProductId).Distinct(StringComparer.Ordinal).ToList();
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
                state.Add(row.Rating, row.CreatedAtOnMp);
                touchedProducts[row.WbProductId] = row;
                continue;
            }

            if (string.Equals(evidence.ReviewHash, hash, StringComparison.Ordinal))
                continue;

            AddEvent(row.WbProductId, row.SourceWbRootId, batchId, row.SourceCategory, row.SourceSubcategory, "reviews", "review_updated", evidence.ReviewHash, hash, evidence.ReviewJson, value, serverReceivedAtUtc);
            state.Replace(evidence.Rating, row.Rating, evidence.CreatedAtOnMp, row.CreatedAtOnMp);
            evidence.Update(row.SourceWbRootId, hash, ToJson(value), row.Rating, row.CreatedAtOnMp, row.ParsedAtUtc, batchId);
            touchedProducts[row.WbProductId] = row;
        }

        foreach (var (productId, first) in touchedProducts)
        {
            var state = summaryStates[productId];
            var value = state.ToValue();
            var hash = Hash(value);
            if (!summaries.TryGetValue(productId, out var current))
            {
                _dbContext.ParserCurrentProductReviewsSummaries.Add(new ParserCurrentProductReviewsSummary(first.WbProductId, first.SourceWbRootId, first.SourceCategory, first.SourceSubcategory, state.ReviewsCount, state.AverageRating, state.RecentNegativeCount, state.LastReviewDateUtc, hash, ToJson(value), first.ParsedAtUtc, batchId));
            }
            else if (!string.Equals(current.ReviewsHash, hash, StringComparison.Ordinal))
            {
                current.Update(state.ReviewsCount, state.AverageRating, state.RecentNegativeCount, state.LastReviewDateUtc, hash, ToJson(value), first.ParsedAtUtc, batchId);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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

    private static string Hash(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static string ToJson(object value) => JsonSerializer.Serialize(value, JsonOptions);

    private static string? JsonText(JsonDocument? value) => value?.RootElement.GetRawText();

    private sealed class ReviewSummaryState
    {
        private int _ratedCount;
        private decimal _ratingSum;

        private ReviewSummaryState(int reviewsCount, int ratedCount, decimal ratingSum, int recentNegativeCount, DateTime? lastReviewDateUtc)
        {
            ReviewsCount = reviewsCount;
            _ratedCount = ratedCount;
            _ratingSum = ratingSum;
            RecentNegativeCount = recentNegativeCount;
            LastReviewDateUtc = lastReviewDateUtc;
        }

        public int ReviewsCount { get; private set; }
        public decimal? AverageRating => _ratedCount == 0 ? null : _ratingSum / _ratedCount;
        public int RecentNegativeCount { get; private set; }
        public DateTime? LastReviewDateUtc { get; private set; }

        public static ReviewSummaryState Empty() => new(0, 0, 0, 0, null);

        public static ReviewSummaryState FromCurrent(ParserCurrentProductReviewsSummary current)
        {
            var ratedCount = current.AverageRating.HasValue && current.ReviewsCount > 0 ? current.ReviewsCount : 0;
            var ratingSum = (current.AverageRating ?? 0) * ratedCount;
            return new ReviewSummaryState(current.ReviewsCount, ratedCount, ratingSum, current.RecentNegativeCount, current.LastReviewDateUtc);
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
        }

        public SortedDictionary<string, object?> ToValue() => new(StringComparer.Ordinal)
        {
            ["reviewsCount"] = ReviewsCount,
            ["averageRating"] = AverageRating,
            ["recentNegativeCount"] = RecentNegativeCount,
            ["lastReviewDateUtc"] = LastReviewDateUtc
        };
    }
}
