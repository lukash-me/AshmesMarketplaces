namespace AshmesMarketplaces.Application.RuleSets.Dtos;

public sealed class RuleSetListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
}
