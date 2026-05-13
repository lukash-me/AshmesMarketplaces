using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Users;

public class Session
{
    private Session() { }

    public Session(
        Guid idUser,
        string? ipAddress,
        string? agent,
        string token,
        int status,
        DateTime dateCreate,
        DateTime? dateRefreshed,
        DateTime dateExpires)
    {
        if (idUser == Guid.Empty)
            throw new ArgumentException("User id is required", nameof(idUser));

        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required", nameof(token));

        DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));
        DateTimeUtc.EnsureUtc(dateRefreshed, nameof(dateRefreshed));
        DateTimeUtc.EnsureUtc(dateExpires, nameof(dateExpires));

        if (dateRefreshed.HasValue && dateRefreshed < dateCreate)
            throw new ArgumentException("DateRefreshed cannot be earlier than DateCreate", nameof(dateRefreshed));

        if (dateExpires < dateCreate)
            throw new ArgumentException("DateExpires cannot be earlier than DateCreate", nameof(dateExpires));

        IdUser = idUser;
        IpAddress = ipAddress;
        Agent = agent;
        Token = token;
        Status = status;
        DateCreate = dateCreate;
        DateRefreshed = dateRefreshed;
        DateExpires = dateExpires;
    }

    public int Id { get; private set; }
    public Guid IdUser { get; private set; }
    public string? IpAddress { get; private set; }
    public string? Agent { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public int Status { get; private set; } // enum по документации, значения не определены
    public DateTime DateCreate { get; private set; }
    public DateTime? DateRefreshed { get; private set; }
    public DateTime DateExpires { get; private set; }
}
