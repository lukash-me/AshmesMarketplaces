namespace AshmesMarketplaces.Application.Auth.Dtos;

public sealed record AccessRequestResponse(
    Guid Id,
    string Contact,
    string Status,
    DateTime CreatedAtUtc);
