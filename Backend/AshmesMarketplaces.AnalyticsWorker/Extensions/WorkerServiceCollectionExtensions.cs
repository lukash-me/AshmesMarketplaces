using AshmesMarketplaces.AnalyticsWorker.Security;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.MarketIntelligence.Services;
using AshmesMarketplaces.Application.MarketRecommendations.Options;
using AshmesMarketplaces.Application.MarketRecommendations.Services;
using AshmesMarketplaces.Application.WorkspaceOverview.Services;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AshmesMarketplaces.AnalyticsWorker.Extensions;

public static class WorkerServiceCollectionExtensions
{
    private const string ConnectionStringName = "Postgres";

    public static IServiceCollection AddWorkerDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is required.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsqlOptions.CommandTimeout(300);
            }));

        return services;
    }

    public static IServiceCollection AddWorkerIntelligenceIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IntelligenceOptions>(
            configuration.GetSection(IntelligenceOptions.SectionName));

        services.AddHttpClient<IIntelligenceClient, IntelligenceClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<IntelligenceOptions>>().Value;
            client.BaseAddress = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
                ? baseUri
                : new Uri(IntelligenceOptions.DefaultBaseUrl);

            var timeoutSeconds = options.TimeoutSeconds > 0
                ? options.TimeoutSeconds
                : IntelligenceOptions.DefaultTimeoutSeconds;
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        services.AddScoped<IMarketHotProductsSnapshotBuilder, MarketHotProductsSnapshotBuilder>();
        services.AddScoped<IMarketHotProductsRecalculationService, MarketHotProductsRecalculationService>();
        services.AddScoped<IMarketHotProductsReadService, MarketHotProductsReadService>();

        return services;
    }

    public static IServiceCollection AddWorkerApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUser, WorkerCurrentUser>();
        services.AddScoped<IPublicMarketIntelligenceReadService, PublicMarketIntelligenceReadService>();
        services.AddScoped<IPublicMarketConcentrationRefreshService, PublicMarketConcentrationRefreshService>();
        services.AddScoped<IPublicTopForecastRefreshService, PublicTopForecastRefreshService>();
        services.AddScoped<IWorkspaceOverviewService, WorkspaceOverviewService>();

        return services;
    }
}
