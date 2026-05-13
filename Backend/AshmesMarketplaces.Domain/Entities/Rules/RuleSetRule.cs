namespace AshmesMarketplaces.Domain.Entities.Rules;

public class RuleSetRule
{
    private RuleSetRule() { }

    public RuleSetRule(Guid idSet, Guid idRule)
    {
        if (idSet == Guid.Empty)
            throw new ArgumentException("Set id is required", nameof(idSet));

        if (idRule == Guid.Empty)
            throw new ArgumentException("Rule id is required", nameof(idRule));

        IdSet = idSet;
        IdRule = idRule;
    }

    public Guid IdSet { get; private set; }
    public Guid IdRule { get; private set; }
}
