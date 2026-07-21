namespace AshmesMarketplaces.Application.Rules.Dtos;

public sealed record RuleResponse(
    Guid Id,
    string Name,
    string? Description,
    string Code,
    int? Number,
    int Domain,
    DateTime DateCreate,
    DateTime DateUpdate);
