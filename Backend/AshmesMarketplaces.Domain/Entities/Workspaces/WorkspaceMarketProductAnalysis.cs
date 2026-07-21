using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Workspaces;

public sealed class WorkspaceMarketProductAnalysis : IDisposable
{
    private WorkspaceMarketProductAnalysis() { }

    public WorkspaceMarketProductAnalysis(
        Guid idAnalysisRun,
        Guid idWorkspaceMarketProduct,
        decimal? currentPrice,
        decimal? previousPrice,
        decimal? priceDelta,
        int? currentPosition,
        int? previousPosition,
        int? positionDelta,
        int? currentStock,
        int? previousStock,
        int? stockDelta,
        int? currentFeedbackCount,
        int? previousFeedbackCount,
        int? feedbackDelta,
        decimal? currentReviewRating,
        decimal? previousReviewRating,
        decimal? reviewRatingDelta,
        DateTime? latestObservedAtUtc,
        JsonDocument signals,
        JsonDocument similarProducts,
        JsonDocument similarProductGroups,
        DateTime computedAtUtc)
    {
        if (idAnalysisRun == Guid.Empty)
            throw new ArgumentException("Analysis run id is required.", nameof(idAnalysisRun));

        if (idWorkspaceMarketProduct == Guid.Empty)
            throw new ArgumentException("Workspace market product id is required.", nameof(idWorkspaceMarketProduct));

        DateTimeUtc.EnsureUtc(latestObservedAtUtc, nameof(latestObservedAtUtc));
        DateTimeUtc.EnsureUtc(computedAtUtc, nameof(computedAtUtc));

        Id = Guid.NewGuid();
        IdAnalysisRun = idAnalysisRun;
        IdWorkspaceMarketProduct = idWorkspaceMarketProduct;
        CurrentPrice = currentPrice;
        PreviousPrice = previousPrice;
        PriceDelta = priceDelta;
        CurrentPosition = currentPosition;
        PreviousPosition = previousPosition;
        PositionDelta = positionDelta;
        CurrentStock = currentStock;
        PreviousStock = previousStock;
        StockDelta = stockDelta;
        CurrentFeedbackCount = currentFeedbackCount;
        PreviousFeedbackCount = previousFeedbackCount;
        FeedbackDelta = feedbackDelta;
        CurrentReviewRating = currentReviewRating;
        PreviousReviewRating = previousReviewRating;
        ReviewRatingDelta = reviewRatingDelta;
        LatestObservedAtUtc = latestObservedAtUtc;
        Signals = signals ?? throw new ArgumentNullException(nameof(signals));
        SimilarProducts = similarProducts ?? throw new ArgumentNullException(nameof(similarProducts));
        SimilarProductGroups = similarProductGroups ?? throw new ArgumentNullException(nameof(similarProductGroups));
        ComputedAtUtc = computedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid IdAnalysisRun { get; private set; }
    public Guid IdWorkspaceMarketProduct { get; private set; }
    public decimal? CurrentPrice { get; private set; }
    public decimal? PreviousPrice { get; private set; }
    public decimal? PriceDelta { get; private set; }
    public int? CurrentPosition { get; private set; }
    public int? PreviousPosition { get; private set; }
    public int? PositionDelta { get; private set; }
    public int? CurrentStock { get; private set; }
    public int? PreviousStock { get; private set; }
    public int? StockDelta { get; private set; }
    public int? CurrentFeedbackCount { get; private set; }
    public int? PreviousFeedbackCount { get; private set; }
    public int? FeedbackDelta { get; private set; }
    public decimal? CurrentReviewRating { get; private set; }
    public decimal? PreviousReviewRating { get; private set; }
    public decimal? ReviewRatingDelta { get; private set; }
    public DateTime? LatestObservedAtUtc { get; private set; }
    public JsonDocument Signals { get; private set; } = null!;
    public JsonDocument SimilarProducts { get; private set; } = null!;
    public JsonDocument SimilarProductGroups { get; private set; } = null!;
    public DateTime ComputedAtUtc { get; private set; }

    public void Dispose()
    {
        Signals?.Dispose();
        SimilarProducts?.Dispose();
        SimilarProductGroups?.Dispose();
    }
}
