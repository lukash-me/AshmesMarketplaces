namespace AshmesMarketplaces.API.Storage;

public sealed class WorkspaceMarketProductMediaStorageOptions
{
    public const string SectionName = "WorkspaceMarketProductMediaStorage";

    public string RootPath { get; init; } = "wwwroot/uploads";
    public string PublicBasePath { get; init; } = "/uploads";
    public long MaxFileBytes { get; init; } = 8 * 1024 * 1024;
}
