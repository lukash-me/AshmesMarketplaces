using AshmesMarketplaces.AnalyticsWorker.Security;
using AshmesMarketplaces.Application.AdminCalculations.Services;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.MarketIntelligence.Services;
using AshmesMarketplaces.Application.MarketplaceCategories.Services;
using AshmesMarketplaces.Application.MarketRecommendations.Options;
using AshmesMarketplaces.Application.MarketRecommendations.Services;
using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.Application.ParserIngestion.Services;
using AshmesMarketplaces.Application.ParserObservability.Services;
using AshmesMarketplaces.Application.WorkspaceOverview.Services;
using AshmesMarketplaces.DataAccess;
using Microsoft.AspNetCore.DataProtection;
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

    public static IServiceCollection AddWorkerApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName(configuration["DataProtection:ApplicationName"] ?? "AshmesMarketplaces");
        var dataProtectionKeysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
        {
            Directory.CreateDirectory(dataProtectionKeysPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
        }

        services.AddSingleton(new ParserLogMonitoringOptions
        {
            LogRoot = ResolveParserLogRoot(configuration),
            StaleAfterSeconds = configuration.GetValue("ParserMonitoring:StaleAfterSeconds", 210)
        });
        services.AddSingleton<IParserLogReader, ParserLogReader>();
        services.AddScoped<IParserRunLogMonitoringService, ParserRunLogMonitoringService>();
        services.AddScoped<ICurrentUser, WorkerCurrentUser>();
        services.AddScoped<IParserIngestionService, ParserIngestionService>();
        services.AddScoped<ParserCompleteBatchPayloadProcessor>();
        services.AddScoped<ParserRankBatchPayloadProcessor>();
        services.AddScoped<ParserProductPresenceReconciliationService>();
        services.AddScoped<IParserBatchPayloadProcessor, ParserBatchPayloadRouter>();
        services.AddScoped<IParserBatchProcessingService, ParserBatchProcessingService>();
        services.AddScoped<IParserLaunchRequestService, ParserLaunchRequestService>();
        services.AddScoped<IParserProxySecretProtector, DataProtectionParserProxySecretProtector>();
        services.AddScoped<IParserProxyManagementService, ParserProxyManagementService>();
        services.AddScoped<IAdminCalculationService, AdminCalculationService>();
        services.AddScoped<IPublicMarketIntelligenceReadService, PublicMarketIntelligenceReadService>();
        services.AddScoped<IPublicMarketIntelligenceRefreshService, PublicMarketIntelligenceRefreshService>();
        services.AddScoped<IPublicMarketConcentrationRefreshService, PublicMarketConcentrationRefreshService>();
        services.AddScoped<IPublicTopForecastRefreshService, PublicTopForecastRefreshService>();
        services.AddScoped<ParserProductReadService>();
        services.AddScoped<ParserObservedMarketEventReadService>();
        services.AddScoped<ParserObservedStockDecreaseReadService>();
        services.AddScoped<IPublicParserCurrentProductRefreshService, PublicParserCurrentProductRefreshService>();
        services.AddScoped<IPublicParserObservedLogisticsRefreshService, PublicParserObservedLogisticsRefreshService>();
        services.AddScoped<IPublicProductAvailabilityRefreshService, PublicProductAvailabilityRefreshService>();
        services.AddScoped<IWorkspaceOverviewService, WorkspaceOverviewService>();
        services.AddHttpClient<WildberriesCategoryCatalogService>();
        services.AddScoped<IWildberriesCategoryCatalogService>(serviceProvider =>
            serviceProvider.GetRequiredService<WildberriesCategoryCatalogService>());
        services.AddScoped<IWildberriesCategoryCatalogRefreshService>(serviceProvider =>
            serviceProvider.GetRequiredService<WildberriesCategoryCatalogService>());

        return services;
    }

    private static string ResolveParserLogRoot(IConfiguration configuration)
    {
        var configured = configuration["ParserMonitoring:LogRoot"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var parserDir = Path.Combine(current.FullName, "Parser");
            if (Directory.Exists(parserDir))
                return Path.Combine(parserDir, "output", "outbox", "_runtime", "logs");

            current = current.Parent;
        }

        return Path.GetFullPath(Path.Combine("Parser", "output", "outbox", "_runtime", "logs"));
    }
}
