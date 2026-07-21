using AshmesMarketplaces.Application.ParserBatches.Services;

namespace AshmesMarketplaces.AnalyticsWorker.Background;

public sealed class ParserRunLogMonitoringHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ParserRunLogMonitoringHostedService> _logger;

    public ParserRunLogMonitoringHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ParserRunLogMonitoringHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IParserRunLogMonitoringService>();
                await service.ProcessOnceAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Parser log monitoring iteration failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
