namespace AshmesMarketplaces.Application.RuleSets.Dtos;

public sealed record RuleSetResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime DateCreate,
    DateTime DateUpdate);
