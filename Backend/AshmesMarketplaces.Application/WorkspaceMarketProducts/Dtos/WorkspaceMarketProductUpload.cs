namespace AshmesMarketplaces.Application.WorkspaceMarketProducts.Dtos;

public sealed record WorkspaceMarketProductUpload(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);
