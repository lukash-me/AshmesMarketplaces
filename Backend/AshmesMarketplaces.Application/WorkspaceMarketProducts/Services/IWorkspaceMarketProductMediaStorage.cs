using AshmesMarketplaces.Application.WorkspaceMarketProducts.Dtos;

namespace AshmesMarketplaces.Application.WorkspaceMarketProducts.Services;

public interface IWorkspaceMarketProductMediaStorage
{
    Task<WorkspaceMarketProductStoredMedia> SaveAsync(
        Guid workspaceId,
        Guid productId,
        WorkspaceMarketProductUpload upload,
        int sortOrder,
        CancellationToken cancellationToken);
}

public sealed record WorkspaceMarketProductStoredMedia(
    string Url,
    string StorageKey,
    string FileName,
    string ContentType);
