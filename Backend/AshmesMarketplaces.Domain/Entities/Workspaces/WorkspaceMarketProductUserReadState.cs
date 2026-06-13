using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Workspaces;

public sealed class WorkspaceMarketProductUserReadState
{
    private WorkspaceMarketProductUserReadState() { }

    public WorkspaceMarketProductUserReadState(
        Guid idWorkspaceMarketProduct,
        Guid idUser,
        DateTime lastViewedAtUtc,
        DateTime? baselineObservedAtUtc,
        decimal? baselinePrice,
        int? baselinePosition,
        int? baselineStock,
        int? baselineFeedbackCount,
        decimal? baselineReviewRating)
    {
        if (idWorkspaceMarketProduct == Guid.Empty)
            throw new ArgumentException("Workspace market product id is required.", nameof(idWorkspaceMarketProduct));

        if (idUser == Guid.Empty)
            throw new ArgumentException("User id is required.", nameof(idUser));

        DateTimeUtc.EnsureUtc(lastViewedAtUtc, nameof(lastViewedAtUtc));
        DateTimeUtc.EnsureUtc(baselineObservedAtUtc, nameof(baselineObservedAtUtc));

        Id = Guid.NewGuid();
        IdWorkspaceMarketProduct = idWorkspaceMarketProduct;
        IdUser = idUser;
        Update(
            lastViewedAtUtc,
            baselineObservedAtUtc,
            baselinePrice,
            baselinePosition,
            baselineStock,
            baselineFeedbackCount,
            baselineReviewRating);
    }

    public Guid Id { get; private set; }
    public Guid IdWorkspaceMarketProduct { get; private set; }
    public Guid IdUser { get; private set; }
    public DateTime LastViewedAtUtc { get; private set; }
    public DateTime? BaselineObservedAtUtc { get; private set; }
    public decimal? BaselinePrice { get; private set; }
    public int? BaselinePosition { get; private set; }
    public int? BaselineStock { get; private set; }
    public int? BaselineFeedbackCount { get; private set; }
    public decimal? BaselineReviewRating { get; private set; }

    public void Update(
        DateTime lastViewedAtUtc,
        DateTime? baselineObservedAtUtc,
        decimal? baselinePrice,
        int? baselinePosition,
        int? baselineStock,
        int? baselineFeedbackCount,
        decimal? baselineReviewRating)
    {
        DateTimeUtc.EnsureUtc(lastViewedAtUtc, nameof(lastViewedAtUtc));
        DateTimeUtc.EnsureUtc(baselineObservedAtUtc, nameof(baselineObservedAtUtc));

        LastViewedAtUtc = lastViewedAtUtc;
        BaselineObservedAtUtc = baselineObservedAtUtc;
        BaselinePrice = baselinePrice;
        BaselinePosition = baselinePosition;
        BaselineStock = baselineStock;
        BaselineFeedbackCount = baselineFeedbackCount;
        BaselineReviewRating = baselineReviewRating;
    }
}
