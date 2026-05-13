namespace AshmesMarketplaces.Domain.Entities.Advertising;

public class CampaignMetric
{
    private CampaignMetric() { }

    public CampaignMetric(
        Guid idCampaign,
        int? impressionAmount,
        int? clicksAmount,
        decimal? costDay,
        DateTime date)
    {
        if (idCampaign == Guid.Empty)
            throw new ArgumentException("Campaign id is required", nameof(idCampaign));

        if (impressionAmount is < 0)
            throw new ArgumentOutOfRangeException(nameof(impressionAmount), "ImpressionAmount must be non-negative");

        if (clicksAmount is < 0)
            throw new ArgumentOutOfRangeException(nameof(clicksAmount), "ClicksAmount must be non-negative");

        if (costDay is < 0)
            throw new ArgumentOutOfRangeException(nameof(costDay), "CostDay must be non-negative");

        Id = Guid.NewGuid();
        IdCampaign = idCampaign;
        ImpressionAmount = impressionAmount;
        ClicksAmount = clicksAmount;
        CostDay = costDay;
        Date = date;
    }

    public Guid Id { get; private set; }
    public Guid IdCampaign { get; private set; }
    public int? ImpressionAmount { get; private set; }
    public int? ClicksAmount { get; private set; }
    public decimal? CostDay { get; private set; }
    public DateTime Date { get; private set; }
}
