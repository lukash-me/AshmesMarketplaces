using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserInstance
{
    private ParserInstance() { }

    public ParserInstance(string parserInstanceId, string? displayName, DateTime seenAtUtc)
    {
        if (string.IsNullOrWhiteSpace(parserInstanceId))
            throw new ArgumentException("Parser instance id is required.", nameof(parserInstanceId));

        DateTimeUtc.EnsureUtc(seenAtUtc, nameof(seenAtUtc));

        Id = Guid.NewGuid();
        ParserInstanceId = parserInstanceId.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        Status = "active";
        LastSeenAtUtc = seenAtUtc;
        CreatedAtUtc = seenAtUtc;
        UpdatedAtUtc = seenAtUtc;
    }

    public Guid Id { get; private set; }
    public string ParserInstanceId { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTime LastSeenAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Touch(DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        LastSeenAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }
}
