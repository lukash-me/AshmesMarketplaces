using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Users;

public class User
{
    private User() { }

    public User(
        Guid idRole,
        string login,
        string password,
        string? email,
        string phone,
        int status,
        DateTime dateCreate,
        DateTime dateLogin)
    {
        if (idRole == Guid.Empty)
            throw new ArgumentException("Role id is required", nameof(idRole));

        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Login is required", nameof(login));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required", nameof(password));

        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone is required", nameof(phone));

        DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));
        DateTimeUtc.EnsureUtc(dateLogin, nameof(dateLogin));

        if (dateLogin < dateCreate)
            throw new ArgumentException("DateLogin cannot be earlier than DateCreate", nameof(dateLogin));

        Id = Guid.NewGuid();
        IdRole = idRole;
        Login = login;
        Password = password;
        Email = email;
        Phone = phone;
        Status = status;
        DateCreate = dateCreate;
        DateLogin = dateLogin;
    }

    public Guid Id { get; private set; }
    public Guid IdRole { get; private set; }
    public string Login { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string Phone { get; private set; } = string.Empty;
    public int Status { get; private set; } // enum по документации, значения не определены
    public DateTime DateCreate { get; private set; }
    public DateTime DateLogin { get; private set; }
}
