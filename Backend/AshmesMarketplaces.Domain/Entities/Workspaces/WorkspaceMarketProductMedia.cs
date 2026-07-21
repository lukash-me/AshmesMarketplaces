using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Workspaces;

public sealed class WorkspaceMarketProductMedia
{
    public const string ImageKind = "image";
    public const string ReferenceKind = "reference";

    private WorkspaceMarketProductMedia() { }

    public WorkspaceMarketProductMedia(
        Guid idWorkspaceMarketProduct,
        string url,
        string storageKey,
        string fileName,
        string contentType,
        int sortOrder,
        string kind,
        DateTime uploadedAtUtc)
    {
        if (idWorkspaceMarketProduct == Guid.Empty)
            throw new ArgumentException("Workspace market product id is required.", nameof(idWorkspaceMarketProduct));

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Url is required.", nameof(url));

        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required.", nameof(contentType));

        if (sortOrder <= 0)
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be positive.");

        if (!IsValidKind(kind))
            throw new ArgumentException("Media kind must be one of: image, reference.", nameof(kind));

        DateTimeUtc.EnsureUtc(uploadedAtUtc, nameof(uploadedAtUtc));

        Id = Guid.NewGuid();
        IdWorkspaceMarketProduct = idWorkspaceMarketProduct;
        Url = url.Trim();
        StorageKey = storageKey.Trim();
        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        SortOrder = sortOrder;
        Kind = kind.Trim();
        UploadedAtUtc = uploadedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid IdWorkspaceMarketProduct { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public string Kind { get; private set; } = ImageKind;
    public DateTime UploadedAtUtc { get; private set; }

    public static bool IsValidKind(string? kind) =>
        string.Equals(kind, ImageKind, StringComparison.Ordinal)
        || string.Equals(kind, ReferenceKind, StringComparison.Ordinal);
}
