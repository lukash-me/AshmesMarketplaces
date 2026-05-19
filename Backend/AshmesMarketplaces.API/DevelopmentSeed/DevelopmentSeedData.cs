namespace AshmesMarketplaces.API.DevelopmentSeed;

public static class DevelopmentSeedData
{
    public static readonly DateTime SeedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    public static readonly IReadOnlyCollection<SeedRole> Roles =
    [
        new("Admin", "Full local development access"),
        new("Manager", "Local development manager account"),
        new("Analyst", "Local development analyst account"),
        new("Viewer", "Local development read-oriented account")
    ];

    public static readonly IReadOnlyCollection<SeedUser> Users =
    [
        new("admin@ashmes.local", "Admin123!", "Admin", "+70000000001"),
        new("manager@ashmes.local", "Manager123!", "Manager", "+70000000002"),
        new("analyst@ashmes.local", "Analyst123!", "Analyst", "+70000000003"),
        new("viewer@ashmes.local", "Viewer123!", "Viewer", "+70000000004")
    ];

    public static readonly IReadOnlyCollection<SeedProduct> Products =
    [
        new("ASH-LOCAL-001", "WB-ASH-001", "Ashmes Premium Organizer", "4680000000001", 12, 2),
        new("ASH-LOCAL-002", "WB-ASH-002", "Ashmes Storage Box", "4680000000002", 15, 2),
        new("ASH-LOCAL-003", "WB-ASH-003", "Ashmes Travel Pouch", "4680000000003", 10, 1)
    ];

    public sealed record SeedRole(string Name, string Description);
    public sealed record SeedUser(string Login, string Password, string RoleName, string Phone);
    public sealed record SeedProduct(
        string SkuSeller,
        string SkuProduct,
        string Name,
        string Barcode,
        int Commission,
        int Status);
}
