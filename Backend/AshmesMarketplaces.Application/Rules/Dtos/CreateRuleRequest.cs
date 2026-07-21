namespace AshmesMarketplaces.Application.Rules.Dtos;

public sealed class CreateRuleRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Code { get; init; } = string.Empty;
    public int? Number { get; init; }
    public int Domain { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
