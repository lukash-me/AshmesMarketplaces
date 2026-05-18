namespace AshmesMarketplaces.Application.RuleSets.Dtos;

public sealed class CreateRuleSetRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
