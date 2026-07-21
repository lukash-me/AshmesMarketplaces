namespace AshmesMarketplaces.Application.RuleSets.Dtos;

public sealed record RuleSetListItemResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime DateCreate,
    DateTime DateUpdate);
