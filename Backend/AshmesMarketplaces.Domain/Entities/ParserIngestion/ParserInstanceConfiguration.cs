using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserInstanceConfiguration
{
    private ParserInstanceConfiguration() { }

    public ParserInstanceConfiguration(
        string parserInstanceId,
        string displayName,
        string hostKind,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(parserInstanceId))
            throw new ArgumentException("Parser instance id is required.", nameof(parserInstanceId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Parser instance display name is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(hostKind))
            throw new ArgumentException("Parser instance host kind is required.", nameof(hostKind));

        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        Id = Guid.NewGuid();
        ParserInstanceId = parserInstanceId.Trim();
        DisplayName = displayName.Trim();
        HostKind = hostKind.Trim();
        Status = ParserInstanceConfigurationStatuses.Active;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public string ParserInstanceId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string HostKind { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<ParserInstanceProxyAssignment> ProxyAssignments { get; private set; } = [];

    public void Update(string displayName, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Parser instance display name is required.", nameof(displayName));

        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        DisplayName = displayName.Trim();
        UpdatedAtUtc = nowUtc;
    }

    public void Disable(DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        Status = ParserInstanceConfigurationStatuses.Inactive;
        UpdatedAtUtc = nowUtc;
        foreach (var assignment in ProxyAssignments)
        {
            assignment.Disable(nowUtc);
        }
    }
}

public static class ParserInstanceConfigurationStatuses
{
    public const string Active = "active";
    public const string Inactive = "inactive";
}

public static class ParserInstanceHostKinds
{
    public const string Local = "local";
}
