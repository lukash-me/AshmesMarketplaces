using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Users;

public sealed class AccessRequest
{
    public const string NewStatus = "new";

    private AccessRequest() { }

    public AccessRequest(
        string contact,
        string comment,
        string? ipAddress,
        string? userAgent,
        string? sourcePath,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(contact))
            throw new ArgumentException("Contact is required.", nameof(contact));

        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        Contact = contact.Trim();
        Comment = comment.Trim();
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath.Trim();
        Status = NewStatus;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Contact { get; private set; } = string.Empty;
    public string Comment { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? SourcePath { get; private set; }
    public string Status { get; private set; } = NewStatus;
    public DateTime CreatedAtUtc { get; private set; }
}
