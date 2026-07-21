using AshmesMarketplaces.Application.MarketRecommendations.Options;
using AshmesMarketplaces.Application.MarketRecommendations.Services;
using Microsoft.Extensions.Options;

namespace AshmesMarketplaces.API.Extensions;

public static class IntelligenceServiceCollectionExtensions
{
    public static IServiceCollection AddIntelligenceIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IntelligenceOptions>(
            configuration.GetSection(IntelligenceOptions.SectionName));

        services.AddHttpClient<IIntelligenceClient, IntelligenceClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<IntelligenceOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
                client.BaseAddress = baseUri;
            else
                client.BaseAddress = new Uri(IntelligenceOptions.DefaultBaseUrl);

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
}
