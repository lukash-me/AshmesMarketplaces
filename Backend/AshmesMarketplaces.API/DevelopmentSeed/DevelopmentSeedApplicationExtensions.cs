using Microsoft.Extensions.Options;

namespace AshmesMarketplaces.API.DevelopmentSeed;

public static class DevelopmentSeedApplicationExtensions
{
    public static async Task SeedDevelopmentDataAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        var options = app.Services.GetRequiredService<IOptions<DevelopmentSeedOptions>>().Value;

        if (!options.EnableDevelopmentSeed)
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>();
        await seeder.SeedAsync();
    }
}
