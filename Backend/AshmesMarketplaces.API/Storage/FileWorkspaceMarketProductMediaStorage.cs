using AshmesMarketplaces.Application.WorkspaceMarketProducts.Dtos;
using AshmesMarketplaces.Application.WorkspaceMarketProducts.Services;
using Microsoft.Extensions.Options;

namespace AshmesMarketplaces.API.Storage;

public sealed class FileWorkspaceMarketProductMediaStorage : IWorkspaceMarketProductMediaStorage
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly WorkspaceMarketProductMediaStorageOptions _options;

    public FileWorkspaceMarketProductMediaStorage(
        IWebHostEnvironment environment,
        IOptions<WorkspaceMarketProductMediaStorageOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    public async Task<WorkspaceMarketProductStoredMedia> SaveAsync(
        Guid workspaceId,
        Guid productId,
        WorkspaceMarketProductUpload upload,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        if (upload.Length <= 0)
            throw new InvalidOperationException("Uploaded file is empty.");

        if (upload.Length > _options.MaxFileBytes)
            throw new InvalidOperationException("Uploaded file is too large.");

        var contentType = string.IsNullOrWhiteSpace(upload.ContentType)
            ? "application/octet-stream"
            : upload.ContentType.Trim();
        if (!AllowedContentTypes.Contains(contentType))
            throw new InvalidOperationException("Only image files are allowed.");

        var extension = ExtensionFromContentType(contentType);
        var safeName = Path.GetFileNameWithoutExtension(upload.FileName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "image";

        var fileName = $"{sortOrder:00}-{Guid.NewGuid():N}{extension}";
        var storageKey = Path.Combine(
                "workspace-products",
                workspaceId.ToString("N"),
                productId.ToString("N"),
                fileName)
            .Replace('\\', '/');

        var root = Path.IsPathRooted(_options.RootPath)
            ? _options.RootPath
            : Path.Combine(_environment.ContentRootPath, _options.RootPath);
        var absolutePath = Path.Combine(root, storageKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var output = File.Create(absolutePath))
        {
            await upload.Content.CopyToAsync(output, cancellationToken);
        }

        var publicBase = "/" + _options.PublicBasePath.Trim('/');
        var url = $"{publicBase}/{storageKey}";
        return new WorkspaceMarketProductStoredMedia(url, storageKey, $"{safeName}{extension}", contentType);
    }

    private static string ExtensionFromContentType(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".bin"
        };
}
