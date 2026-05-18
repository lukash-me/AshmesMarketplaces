namespace AshmesMarketplaces.Application.Rules.Dtos;

public sealed class RuleListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public int? Domain { get; init; }
    public int? Number { get; init; }
}
