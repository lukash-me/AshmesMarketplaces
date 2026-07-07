using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserProxy
{
    private ParserProxy() { }

    public ParserProxy(
        string ip,
        int httpPort,
        int socksPort,
        string login,
        string encryptedPassword,
        DateTime nowUtc)
        : this(null, ip, httpPort, socksPort, login, encryptedPassword, nowUtc)
    {
    }

    public ParserProxy(
        string? key,
        string ip,
        int httpPort,
        int socksPort,
        string login,
        string encryptedPassword,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(ip))
            throw new ArgumentException("Proxy IP is required.", nameof(ip));
        if (httpPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(httpPort), "HTTP port must be between 1 and 65535.");
        if (socksPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(socksPort), "SOCKS5 port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Proxy login is required.", nameof(login));
        if (string.IsNullOrWhiteSpace(encryptedPassword))
            throw new ArgumentException("Proxy encrypted password is required.", nameof(encryptedPassword));

        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        Id = Guid.NewGuid();
        Key = string.IsNullOrWhiteSpace(key) ? Id.ToString("D") : key.Trim();
        Ip = ip.Trim();
        HttpPort = httpPort;
        SocksPort = socksPort;
        Login = login.Trim();
        EncryptedPassword = encryptedPassword;
        Status = ParserProxyStatuses.Active;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Ip { get; private set; } = string.Empty;
    public int HttpPort { get; private set; }
    public int SocksPort { get; private set; }
    public string Login { get; private set; } = string.Empty;
    public string EncryptedPassword { get; private set; } = string.Empty;
    public string Status { get; private set; } = ParserProxyStatuses.Active;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public ParserProxyNicheAssignment? Assignment { get; private set; }

    public void Update(
        string ip,
        int httpPort,
        int socksPort,
        string login,
        string? encryptedPassword,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(ip))
            throw new ArgumentException("Proxy IP is required.", nameof(ip));
        if (httpPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(httpPort), "HTTP port must be between 1 and 65535.");
        if (socksPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(socksPort), "SOCKS5 port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Proxy login is required.", nameof(login));

        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        Ip = ip.Trim();
        HttpPort = httpPort;
        SocksPort = socksPort;
        Login = login.Trim();
        if (!string.IsNullOrWhiteSpace(encryptedPassword))
            EncryptedPassword = encryptedPassword;
        UpdatedAtUtc = nowUtc;
    }

    public void Disable(DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        Status = ParserProxyStatuses.Inactive;
        UpdatedAtUtc = nowUtc;
        Assignment?.Disable(nowUtc);
    }
}

public static class ParserProxyStatuses
{
    public const string Active = "active";
    public const string Inactive = "inactive";
}
