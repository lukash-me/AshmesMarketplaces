namespace AshmesMarketplaces.Application.RuleSetRules.Dtos;

public sealed class RuleSetRuleListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdSet { get; init; }
    public Guid? IdRule { get; init; }
}
