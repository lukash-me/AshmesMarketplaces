namespace AshmesMarketplaces.Application.Auth.Dtos;

public sealed class CreateAccessRequestRequest
{
    public string Contact { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public string? SourcePath { get; init; }
}
