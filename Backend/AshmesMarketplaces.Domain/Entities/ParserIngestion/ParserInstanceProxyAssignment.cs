using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserInstanceProxyAssignment
{
    private ParserInstanceProxyAssignment() { }

    public ParserInstanceProxyAssignment(Guid parserInstanceConfigurationId, Guid proxyId, DateTime nowUtc)
    {
        if (parserInstanceConfigurationId == Guid.Empty)
            throw new ArgumentException("Parser instance configuration id is required.", nameof(parserInstanceConfigurationId));
        if (proxyId == Guid.Empty)
            throw new ArgumentException("Proxy id is required.", nameof(proxyId));

        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        Id = Guid.NewGuid();
        ParserInstanceConfigurationId = parserInstanceConfigurationId;
        ProxyId = proxyId;
        Enabled = true;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public Guid ParserInstanceConfigurationId { get; private set; }
    public Guid ProxyId { get; private set; }
    public bool Enabled { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public ParserInstanceConfiguration? ParserInstanceConfiguration { get; private set; }
    public ParserProxy? Proxy { get; private set; }

    public void Disable(DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        Enabled = false;
        UpdatedAtUtc = nowUtc;
    }
}
