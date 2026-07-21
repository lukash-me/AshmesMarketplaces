using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AshmesMarketplaces.DataAccess;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string ConnectionStringName = "Postgres";
    private const string EnvironmentConnectionStringKey = "ConnectionStrings__Postgres";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsqlOptions.CommandTimeout(300);
            })
            .Options;

        return new ApplicationDbContext(options);
    }

    private static string ResolveConnectionString()
    {
        var environmentValue = Environment.GetEnvironmentVariable(EnvironmentConnectionStringKey)
            ?? Environment.GetEnvironmentVariable($"ConnectionStrings:{ConnectionStringName}");

        if (!string.IsNullOrWhiteSpace(environmentValue))
            return environmentValue;

        var apiProjectDirectory = FindApiProjectDirectory();
        var developmentSettings = Path.Combine(apiProjectDirectory, "appsettings.Development.json");
        var baseSettings = Path.Combine(apiProjectDirectory, "appsettings.json");

        return TryReadConnectionString(developmentSettings)
            ?? TryReadConnectionString(baseSettings)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is required. Set '{EnvironmentConnectionStringKey}' or configure API appsettings.");
    }

    private static string FindApiProjectDirectory()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startPath);

            while (directory is not null)
            {
                if (directory.Name == "AshmesMarketplaces.API")
                    return directory.FullName;

                var candidate = Path.Combine(directory.FullName, "Backend", "AshmesMarketplaces.API");
                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Unable to locate AshmesMarketplaces.API project directory.");
    }

    private static string? TryReadConnectionString(string settingsPath)
    {
        if (!File.Exists(settingsPath))
            return null;

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));

        if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
            return null;

        if (!connectionStrings.TryGetProperty(ConnectionStringName, out var connectionString))
            return null;

        var value = connectionString.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
