using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Marketplaces;

public sealed class PublicTopForecastPrediction
{
    private PublicTopForecastPrediction() { }

    public PublicTopForecastPrediction(
        Guid idRun,
        string sourceCategory,
        string sourceSubcategory,
        string query,
        string sourceRegionDest,
        string sort,
        int topN,
        string wbProductId,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(sourceCategory))
            throw new ArgumentException("Source category is required.", nameof(sourceCategory));
        if (string.IsNullOrWhiteSpace(sourceSubcategory))
            throw new ArgumentException("Source subcategory is required.", nameof(sourceSubcategory));
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query is required.", nameof(query));
        if (string.IsNullOrWhiteSpace(sourceRegionDest))
            throw new ArgumentException("Source region destination is required.", nameof(sourceRegionDest));
        if (string.IsNullOrWhiteSpace(sort))
            throw new ArgumentException("Sort is required.", nameof(sort));
        if (string.IsNullOrWhiteSpace(wbProductId))
            throw new ArgumentException("WB product id is required.", nameof(wbProductId));

        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        IdRun = idRun;
        SourceCategory = sourceCategory.Trim();
        SourceSubcategory = sourceSubcategory.Trim();
        Query = query.Trim();
        SourceRegionDest = sourceRegionDest.Trim();
        Sort = sort.Trim();
        TopN = topN;
        WbProductId = wbProductId.Trim();
        ReasonsJson = "[]";
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid IdRun { get; private set; }
    public PublicTopForecastRun? Run { get; private set; }
    public string SourceCategory { get; private set; } = string.Empty;
    public string SourceSubcategory { get; private set; } = string.Empty;
    public string Query { get; private set; } = string.Empty;
    public string SourceRegionDest { get; private set; } = string.Empty;
    public string Sort { get; private set; } = string.Empty;
    public int TopN { get; private set; }
    public Guid? ProductRowId { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public string? ProductName { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public decimal? Price { get; private set; }
    public decimal? Rating { get; private set; }
    public int? FeedbackCount { get; private set; }
    public int? Stock { get; private set; }
    public int? CurrentPosition { get; private set; }
    public string? CurrentPositionState { get; private set; }
    public int? ObservedRangeLimit { get; private set; }
    public int? PredictedPosition { get; private set; }
    public decimal Top100Probability { get; private set; }
    public decimal Confidence { get; private set; }
    public decimal FeatureCoveragePercent { get; private set; }
    public string? SellerName { get; private set; }
    public string? BrandName { get; private set; }
    public string ReasonsJson { get; private set; } = "[]";
    public DateTime CreatedAtUtc { get; private set; }

    public void SetProductSnapshot(
        Guid productRowId,
        string? wbRootId,
        string? productName,
        string? thumbnailUrl,
        decimal? price,
        decimal? rating,
        int? feedbackCount,
        int? stock,
        int? currentPosition,
        string? currentPositionState,
        int? observedRangeLimit,
        string? sellerName,
        string? brandName)
    {
        ProductRowId = productRowId;
        WbRootId = string.IsNullOrWhiteSpace(wbRootId) ? null : wbRootId.Trim();
        ProductName = string.IsNullOrWhiteSpace(productName) ? null : productName.Trim();
        ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl.Trim();
        Price = price;
        Rating = rating;
        FeedbackCount = feedbackCount;
        Stock = stock;
        CurrentPosition = currentPosition;
        CurrentPositionState = string.IsNullOrWhiteSpace(currentPositionState) ? null : currentPositionState.Trim();
        ObservedRangeLimit = observedRangeLimit;
        SellerName = string.IsNullOrWhiteSpace(sellerName) ? null : sellerName.Trim();
        BrandName = string.IsNullOrWhiteSpace(brandName) ? null : brandName.Trim();
    }

    public void SetPrediction(
        int? predictedPosition,
        decimal top100Probability,
        decimal confidence,
        decimal featureCoveragePercent,
        string reasonsJson)
    {
        PredictedPosition = predictedPosition;
        Top100Probability = top100Probability;
        Confidence = confidence;
        FeatureCoveragePercent = featureCoveragePercent;
        ReasonsJson = string.IsNullOrWhiteSpace(reasonsJson) ? "[]" : reasonsJson;
    }
}
